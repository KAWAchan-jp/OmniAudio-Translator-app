namespace OmniAudioTranslator.Core.Capture;

/// <summary>
/// PCMストリームを一定間隔(既定6秒)ごとにチャンク化し、無音チャンクを破棄してWAVとして通知する。
/// Stream-Speech-Layerの offscreen.js の startRecorderCycle()/resetChunkState() を、
/// MediaRecorderの開始/停止サイクルから<see cref="Timer"/>ベースのバッファスワップへ置き換えたもの。
/// 発話が検出されたチャンクは<see cref="AudioNormalizer"/>でピーク正規化してからWAV化する。
/// 配信側の音量が小さくても認識できるようにするための処理(詳細は<see cref="AudioNormalizer"/>)。
/// </summary>
public sealed class AudioChunker : IDisposable
{
    /// <summary>チャンク間隔の既定値(ミリ秒)。UIの「初期値に戻す」でも使う。</summary>
    public const int DefaultChunkMillis = 6000;

    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly int _bitsPerSample;
    private readonly int _chunkMillis;
    private readonly LevelMeter _levelMeter;
    private readonly object _lock = new();
    private readonly List<byte> _buffer = [];
    private Timer? _timer;

    public AudioChunker(
        int sampleRate,
        int channels,
        int bitsPerSample,
        int chunkMillis = DefaultChunkMillis,
        int vadThresholdPercent = AudioNormalizer.DefaultVadThresholdPercent)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _bitsPerSample = bitsPerSample;
        _chunkMillis = chunkMillis;
        _levelMeter = new LevelMeter(vadThresholdPercent);
    }

    /// <summary>
    /// 発話が検出されたチャンクが確定した際に発火する(wavBytes, peakLevel, gainDb)。
    /// peakLevelは正規化前の生のピーク(%)で入力レベルの確認用、gainDbは適用した増幅量。
    /// </summary>
    public event Action<byte[], int, double>? ChunkReady;

    /// <summary>無音のため破棄されたチャンクの際に発火する(peakLevel)。</summary>
    public event Action<int>? ChunkSkipped;

    public void Start()
    {
        lock (_lock)
        {
            _buffer.Clear();
            _levelMeter.Reset();
        }

        _timer = new Timer(_ => Flush(), null, _chunkMillis, _chunkMillis);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void AddSamples(ReadOnlySpan<byte> buffer)
    {
        lock (_lock)
        {
            _levelMeter.ProcessSamples(buffer);
            _buffer.AddRange(buffer);
        }
    }

    /// <summary>
    /// 発話ありと判定するピーク閾値(%)を変更する。小さい音声を拾いたいときにキャプチャ中でも変更できる。
    /// 次に処理されるサンプルから適用され、停止や再開始は不要。
    /// </summary>
    public void SetVadThresholdPercent(int thresholdPercent) => _levelMeter.ThresholdPercent = thresholdPercent;

    private void Flush()
    {
        byte[] pcm;
        int peakLevel;
        bool hadSpeech;

        lock (_lock)
        {
            pcm = [.. _buffer];
            peakLevel = _levelMeter.MaxLevel;
            hadSpeech = _levelMeter.HadSpeech;
            _buffer.Clear();
            _levelMeter.Reset();
        }

        if (!hadSpeech || pcm.Length == 0)
        {
            ChunkSkipped?.Invoke(peakLevel);
            return;
        }

        AudioNormalizationResult normalized = AudioNormalizer.Normalize(pcm);
        byte[] wav = WavEncoder.Encode(normalized.Pcm, _sampleRate, _channels, _bitsPerSample);
        ChunkReady?.Invoke(wav, peakLevel, normalized.GainDb);
    }

    public void Dispose() => Stop();
}
