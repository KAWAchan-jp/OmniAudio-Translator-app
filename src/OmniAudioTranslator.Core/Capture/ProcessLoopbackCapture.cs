using System.Runtime.InteropServices;
using OmniAudioTranslator.Core.Interop;
using Vanara.PInvoke;
using static Vanara.PInvoke.CoreAudio;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.WinMm;

namespace OmniAudioTranslator.Core.Capture;

/// <summary>
/// Windows 10 Build 20348+のProcess Loopback Capture API(ActivateAudioInterfaceAsync +
/// AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK)を使い、指定プロセスが再生する音声だけをキャプチャする。
/// 実装はMicrosoft公式サンプル(microsoft/Windows-classic-samples, Samples/ApplicationLoopback)の設計を踏襲し、
/// GetMixFormatは呼ばず16bit PCM/2ch/44100Hzで決め打ちする。
/// </summary>
public sealed class ProcessLoopbackCapture : IProcessAudioCapture
{
    public event Action<byte[], int>? DataAvailable;

    /// <summary>キャプチャスレッドが例外で停止したときに発火する(UIへ通知するため)。</summary>
    public event Action<Exception>? CaptureFailed;

    public int SampleRate { get; } = 44100;
    public int Channels { get; } = 2;
    public int BitsPerSample { get; } = 16;

    private IAudioClient? _audioClient;
    private IAudioCaptureClient? _captureClient;
    private AutoResetEvent? _sampleReadyEvent;
    private Thread? _captureThread;
    private volatile bool _stopping;

    public Task StartAsync(int processId, bool includeProcessTree = true, CancellationToken cancellationToken = default)
    {
        if (_captureThread is not null)
        {
            throw new InvalidOperationException("音声キャプチャは既に開始されています。");
        }

        _stopping = false;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _captureThread = new Thread(() => CaptureThreadMain(processId, includeProcessTree, started))
        {
            IsBackground = true,
            Name = "ProcessLoopbackCapture",
        };
        _captureThread.SetApartmentState(ApartmentState.MTA);
        _captureThread.Start();

        return started.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Process Loopback用COMオブジェクトの所有スレッド。アクティブ化、初期化、読み出しを同じMTAで行う。
    /// IAudioCaptureClientを別スレッドで使うと、停止後の再開始でE_NOINTERFACEになることを実機で確認した。
    /// </summary>
    private void CaptureThreadMain(int processId, bool includeProcessTree, TaskCompletionSource started)
    {
        try
        {
            InitializeAudioClient(processId, includeProcessTree);
            started.TrySetResult();
            CaptureLoop();
        }
        catch (Exception ex)
        {
            if (!started.TrySetException(ex) && !_stopping)
            {
                CaptureFailed?.Invoke(ex);
            }
        }
    }

    private void InitializeAudioClient(int processId, bool includeProcessTree)
    {
        var handler = new ActivateAudioInterfaceCompletionHandler();

        var activationParams = new ActivationParamsBlob
        {
            ActivationType = AUDIOCLIENT_ACTIVATION_TYPE.AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK,
            TargetProcessId = (uint)processId,
            ProcessLoopbackMode = includeProcessTree
                ? PROCESS_LOOPBACK_MODE.PROCESS_LOOPBACK_MODE_INCLUDE_TARGET_PROCESS_TREE
                : PROCESS_LOOPBACK_MODE.PROCESS_LOOPBACK_MODE_EXCLUDE_TARGET_PROCESS_TREE,
        };

        // ActivateAudioInterfaceAsyncは非同期操作であり、Windows(mmdevapi)はActivateCompleted
        // コールバックが発火するまでactivationParamsのBLOBメモリを参照し続ける。
        // PROPVARIANT.Dispose()はVT_BLOBの場合PropVariantClear経由でCoTaskMemFreeを呼ぶため、
        // blobPtrはAllocCoTaskMemで確保する(アロケータ不一致はヒープ破損の原因になる。実機検証で確認済み)。
        // 所有権はpropvariantに渡り、そのDispose()がblobPtrの解放を担うため、ここでは明示的にFreeしない。
        int blobSize = Marshal.SizeOf<ActivationParamsBlob>();
        IntPtr blobPtr = Marshal.AllocCoTaskMem(blobSize);
        Marshal.StructureToPtr(activationParams, blobPtr, false);

        var blobValue = new BLOB { cbSize = (uint)blobSize, pBlobData = blobPtr };
        IActivateAudioInterfaceAsyncOperation operation;
        using (var propvariant = new PROPVARIANT(blobValue, VarEnum.VT_BLOB))
        {
            ActivateAudioInterfaceAsyncNative.Activate(
                ProcessLoopbackDevice.VirtualAudioDeviceProcessLoopback,
                typeof(IAudioClient).GUID,
                propvariant,
                handler,
                out _)
                .ThrowIfFailed();

            // 実際に使うIActivateAudioInterfaceAsyncOperationはActivateCompletedコールバック経由で得る。
            // awaitすると継続先が別スレッドになり、IAudioCaptureClientの所有スレッドが分かれてしまう。
            // このMTAスレッドで完了を待ち、そのまま初期化とキャプチャを続ける。
            operation = handler.Completion.GetAwaiter().GetResult();
        }

        operation.GetActivateResult(out HRESULT activateResult, out object? activatedInterface).ThrowIfFailed();
        activateResult.ThrowIfFailed();

        _audioClient = (IAudioClient)activatedInterface!;

        var format = new WAVEFORMATEX
        {
            wFormatTag = (WAVE_FORMAT)1, // WAVE_FORMAT_PCM (Vanara.PInvoke.WinMm.WAVE_FORMATに定数が無いため数値指定)
            nChannels = (ushort)Channels,
            nSamplesPerSec = (uint)SampleRate,
            wBitsPerSample = (ushort)BitsPerSample,
        };
        format.nBlockAlign = (ushort)(format.nChannels * format.wBitsPerSample / 8);
        format.nAvgBytesPerSec = format.nSamplesPerSec * format.nBlockAlign;

        int formatSize = Marshal.SizeOf<WAVEFORMATEX>();
        IntPtr formatPtr = Marshal.AllocHGlobal(formatSize);
        try
        {
            Marshal.StructureToPtr(format, formatPtr, false);
            _audioClient.Initialize(
                AUDCLNT_SHAREMODE.AUDCLNT_SHAREMODE_SHARED,
                AUDCLNT_STREAMFLAGS.AUDCLNT_STREAMFLAGS_LOOPBACK
                    | AUDCLNT_STREAMFLAGS.AUDCLNT_STREAMFLAGS_EVENTCALLBACK
                    | AUDCLNT_STREAMFLAGS.AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM,
                0,
                0,
                formatPtr,
                Guid.Empty)
                .ThrowIfFailed();
        }
        finally
        {
            Marshal.FreeHGlobal(formatPtr);
        }

        _captureClient = (IAudioCaptureClient)_audioClient.GetService(typeof(IAudioCaptureClient).GUID)!;

        _sampleReadyEvent = new AutoResetEvent(false);
        _audioClient.SetEventHandle(_sampleReadyEvent);

        _audioClient.Start();
    }

    private void CaptureLoop()
    {
        // このメソッドは専用スレッドのエントリポイントであり、ここから例外が抜けるとバックグラウンドスレッドの
        // 未処理例外としてプロセスごと即死する。停止処理との競合でCOMオブジェクトが解放済みになるケースが
        // 実際にあった(停止→開始でInvalidCastException/E_NOINTERFACEによりクラッシュ)ため、
        // ループ全体を必ず捕捉して静かに抜ける。
        try
        {
            int blockAlign = Channels * BitsPerSample / 8;

            // 停止処理がフィールドをnullにするのと競合しないよう、ループ中はローカルに束ねて使う。
            AutoResetEvent? sampleReadyEvent = _sampleReadyEvent;
            IAudioCaptureClient? captureClient = _captureClient;
            if (sampleReadyEvent is null || captureClient is null)
            {
                return;
            }

            while (!_stopping)
            {
                if (!sampleReadyEvent.WaitOne(200))
                {
                    continue;
                }

                while (!_stopping
                    && captureClient.GetNextPacketSize(out uint framesAvailable).Succeeded
                    && framesAvailable > 0)
                {
                    HRESULT hr = captureClient.GetBuffer(
                        out IntPtr dataPtr,
                        out uint numFrames,
                        out AUDCLNT_BUFFERFLAGS _,
                        out _,
                        out _);

                    if (hr.Failed)
                    {
                        break;
                    }

                    int byteCount = (int)numFrames * blockAlign;
                    if (byteCount > 0)
                    {
                        byte[] buffer = new byte[byteCount];
                        Marshal.Copy(dataPtr, buffer, 0, byteCount);
                        DataAvailable?.Invoke(buffer, byteCount);
                    }

                    captureClient.ReleaseBuffer(numFrames);
                }
            }
        }
        catch (Exception) when (_stopping)
        {
            // Stop()と同時に進行中だったCOM呼び出しはE_NOINTERFACE等で失敗し得る。
            // 停止を要求した結果なので、利用者に「キャプチャが停止した」エラーとして通知しない。
        }
        catch (Exception ex)
        {
            CaptureFailed?.Invoke(ex);
        }
    }

    public void Stop()
    {
        _stopping = true;

        // WaitOne(200)のタイムアウト待ちを挟まず即座に起こしてループを抜けさせる。
        AutoResetEvent? sampleReadyEvent = _sampleReadyEvent;
        try
        {
            sampleReadyEvent?.Set();
        }
        catch (ObjectDisposedException)
        {
            // 既に破棄済みなら起こす必要も無い
        }

        // COMオブジェクトやイベントを解放する前に、キャプチャスレッドの終了を必ず待ち切る。
        // ここで待たずに解放すると、生き残ったスレッドが解放済みCOMを触ってプロセスごと落ちる
        // (停止→開始でのクラッシュの直接原因だった)。DataAvailableハンドラ側の処理時間も考慮し
        // 十分長く待つが、万一固まっても停止操作自体は返せるよう上限は設ける。
        Thread? captureThread = _captureThread;
        _captureThread = null;
        captureThread?.Join(TimeSpan.FromSeconds(10));

        try
        {
            _audioClient?.Stop();
        }
        catch
        {
            // 停止時点で既に無効化されている場合は無視する
        }

        _sampleReadyEvent = null;
        _captureClient = null;
        _audioClient = null;
        sampleReadyEvent?.Dispose();
    }

    public void Dispose() => Stop();
}
