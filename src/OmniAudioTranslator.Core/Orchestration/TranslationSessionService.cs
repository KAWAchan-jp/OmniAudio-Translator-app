using OmniAudioTranslator.Core.Capture;
using OmniAudioTranslator.Core.Localization;
using OmniAudioTranslator.Core.Transcription;
using OmniAudioTranslator.Core.Translation;

namespace OmniAudioTranslator.Core.Orchestration;

/// <summary>
/// Capture→Chunker→ASR→翻訳の一連のパイプラインを制御する。
/// 翻訳プロバイダが未指定の場合は認識結果をそのまま通知する(Phase4の社内MVPラインとの後方互換)。
/// </summary>
public sealed class TranslationSessionService : IDisposable
{
    private readonly IProcessAudioCapture _capture;
    private readonly AudioChunker _chunker;
    private readonly ITranscriptionProvider _transcriptionProvider;
    private ITranslationProvider? _translationProvider;
    // UIから変更される。各チャンクの処理開始時に読み取ることで、進行中の認識を中断せず次のチャンクから反映する。
    private string _targetLanguage;
    private string _sourceLanguage;

    // ASRエンジン(WhisperNetProvider)は1回に1リクエストずつ処理する前提のため、チャンクの認識を直列化する。
    // 未直列化のままだと新しいチャンクが次々にASR呼び出しへ流れ込み、ネイティブ実装側の同時アクセスにより
    // 応答が返らなくなる不具合を実機で確認した。処理が追いつかない場合は新しいチャンクを間引いて破棄する
    // (ライブ字幕用途では古いチャンクを溜め込むより、間引いて最新に追従する方が望ましい)。
    private readonly SemaphoreSlim _transcriptionGate = new(1, 1);

    // Dispose後に遅れて届いたチャンクを処理しないための印。
    private volatile bool _stopped;

    public TranslationSessionService(
        IProcessAudioCapture capture,
        AudioChunker chunker,
        ITranscriptionProvider transcriptionProvider,
        ITranslationProvider? translationProvider = null,
        string targetLanguage = "ja",
        string sourceLanguage = "auto")
    {
        _capture = capture;
        _chunker = chunker;
        _transcriptionProvider = transcriptionProvider;
        _translationProvider = translationProvider;
        _targetLanguage = targetLanguage;
        _sourceLanguage = sourceLanguage;

        _capture.DataAvailable += OnDataAvailable;
        _capture.CaptureFailed += OnCaptureFailed;
        _chunker.ChunkReady += OnChunkReady;
        _chunker.ChunkSkipped += peakLevel => StatusChanged?.Invoke(Strings.Format("session.silence_skipped", peakLevel));
    }

    /// <summary>
    /// 認識結果のテキストが確定するたびに発火する。第1引数は認識結果(元言語)、第2引数は翻訳結果
    /// (翻訳プロバイダ未指定または翻訳失敗時はnull)。
    /// </summary>
    public event Action<string, string?>? TranscriptReady;

    /// <summary>状態文言が変化するたびに発火する(UIのステータス表示用)。</summary>
    public event Action<string>? StatusChanged;

    public async Task StartAsync(int processId, CancellationToken cancellationToken = default)
    {
        StatusChanged?.Invoke(Strings.Get("session.starting_capture"));
        await _capture.StartAsync(processId, cancellationToken: cancellationToken).ConfigureAwait(false);
        _chunker.Start();
        StatusChanged?.Invoke(Strings.Get("session.recognizing"));
    }

    public void Stop()
    {
        _chunker.Stop();
        _capture.Stop();
        StatusChanged?.Invoke(Strings.Get("session.stopped"));
    }

    /// <summary>
    /// 認識元言語と翻訳先言語を更新する。現在処理中のチャンクは従来設定のまま完了させ、
    /// 次に処理を開始するチャンクから新しい設定を使うため、キャプチャの停止・再開始は不要である。
    /// </summary>
    public void UpdateLanguages(string sourceLanguage, string targetLanguage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLanguage);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguage);
        Volatile.Write(ref _sourceLanguage, sourceLanguage);
        Volatile.Write(ref _targetLanguage, targetLanguage);
    }

    /// <summary>翻訳プロバイダを更新する。次に処理を開始するチャンクから新しいプロバイダを使う。</summary>
    public void UpdateTranslationProvider(ITranslationProvider? translationProvider) =>
        Volatile.Write(ref _translationProvider, translationProvider);

    private void OnDataAvailable(byte[] buffer, int count) => _chunker.AddSamples(buffer.AsSpan(0, count));

    private void OnCaptureFailed(Exception ex) =>
        StatusChanged?.Invoke(Strings.Format("session.capture_failed", ex.Message));

    // async voidのため、ここから例外が抜けるとプロセスごと落ちる。停止処理と競合して
    // 破棄済みリソースに触れるケース(ObjectDisposedException)で実際にクラッシュしたため、
    // メソッド全体を必ず捕捉する。
    private async void OnChunkReady(byte[] wavBytes, int peakLevel)
    {
        try
        {
            await ProcessChunkAsync(wavBytes).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // 停止処理と競合しただけなので無視する(セッションは既に終了している)
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(Strings.Format("session.recognition_error", ex.Message));
        }
    }

    private async Task ProcessChunkAsync(byte[] wavBytes)
    {
        if (_stopped || !await _transcriptionGate.WaitAsync(0).ConfigureAwait(false))
        {
            StatusChanged?.Invoke(Strings.Get("session.chunk_dropped_busy"));
            return;
        }

        try
        {
            StatusChanged?.Invoke(Strings.Format("session.recognizing_chunk", wavBytes.Length / 1024));

            // チャンクごとにスナップショットを取る。UI操作と競合しても1チャンク内の設定は一貫させる。
            string sourceLanguage = Volatile.Read(ref _sourceLanguage);
            string targetLanguage = Volatile.Read(ref _targetLanguage);
            ITranslationProvider? translationProvider = Volatile.Read(ref _translationProvider);

            using var stream = new MemoryStream(wavBytes);
            TranscriptionResult result = await _transcriptionProvider.TranscribeAsync(stream, sourceLanguage).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(result.Text))
            {
                StatusChanged?.Invoke(Strings.Get("session.empty_result"));
                return;
            }

            if (HallucinationFilter.IsLikelyHallucination(result.Text))
            {
                StatusChanged?.Invoke(Strings.Get("session.hallucination_discarded"));
                return;
            }

            string? translatedText = null;

            if (translationProvider is not null)
            {
                try
                {
                    StatusChanged?.Invoke(Strings.Get("session.translating"));
                    translatedText = await translationProvider.TranslateAsync(result.Text, targetLanguage).ConfigureAwait(false);
                }
                catch (TranslationProviderException ex)
                {
                    StatusChanged?.Invoke(ex.IsRateLimited
                        ? Strings.Get("session.translation_rate_limited")
                        : Strings.Format("session.translation_error", ex.Message));
                }
            }

            // 訳文が原文と完全一致した場合、オーバーレイは同じ文を二重に出さないよう1行だけ表示する。
            // ただし黙って原文行が消えると「原文が表示されない」不具合に見えるため、宛先言語が音声の言語と
            // 同じ可能性がある旨を明示する(実際にユーザーが誤解した経緯があるため)。
            if (translationProvider is not null
                && !string.IsNullOrEmpty(translatedText)
                && translatedText == result.Text)
            {
                StatusChanged?.Invoke(Strings.Get("session.translation_identical"));
            }
            else
            {
                StatusChanged?.Invoke(Strings.Get("session.result_shown"));
            }

            TranscriptReady?.Invoke(result.Text, translatedText);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(Strings.Format("session.recognition_error", ex.Message));
        }
        finally
        {
            _transcriptionGate.Release();
        }
    }

    public void Dispose()
    {
        _stopped = true;
        Stop();
        _capture.DataAvailable -= OnDataAvailable;
        _capture.CaptureFailed -= OnCaptureFailed;
        _chunker.ChunkReady -= OnChunkReady;
        _chunker.Dispose();
        _capture.Dispose();

        // _transcriptionGateはあえてDispose()しない。停止時にまだ認識中のチャンクが残っていると、
        // その完了時のRelease()がObjectDisposedExceptionになりasync void経由でプロセスごと落ちるため
        // (実機で確認済み)。GC任せで十分な軽量オブジェクトなので破棄しない方が安全。
    }
}
