namespace OmniAudioTranslator.Core.Capture;

/// <summary>指定プロセスが再生する音声だけをキャプチャする機能。</summary>
public interface IProcessAudioCapture : IDisposable
{
    /// <summary>PCMデータが利用可能になるたびに発火する(buffer, byteCount)。</summary>
    event Action<byte[], int>? DataAvailable;

    /// <summary>キャプチャが例外で停止したときに発火する。</summary>
    event Action<Exception>? CaptureFailed;

    int SampleRate { get; }
    int Channels { get; }
    int BitsPerSample { get; }

    /// <summary>
    /// 指定プロセスの音声キャプチャを開始する。
    /// </summary>
    /// <param name="processId">対象プロセスID。</param>
    /// <param name="includeProcessTree">子プロセスの音声も含めるか。</param>
    Task StartAsync(int processId, bool includeProcessTree = true, CancellationToken cancellationToken = default);

    void Stop();
}
