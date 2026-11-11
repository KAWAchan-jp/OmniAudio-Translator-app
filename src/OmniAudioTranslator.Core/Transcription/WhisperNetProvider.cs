using System.Text;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OmniAudioTranslator.Core.Localization;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace OmniAudioTranslator.Core.Transcription;

/// <summary>
/// Whisper.net(whisper.cppのC#バインディング)によるローカルASR。Pythonサイドカーを使わず.NETプロセス内で完結する。
/// Whisper.netは16kHzのWAVしか受け付けないため、入力WAVを都度16kHzへリサンプルしてから渡す
/// (公式のNAudioResampleWavサンプルと同じ方式)。
/// AllRuntimesパッケージによりCPU/CUDA/CUDA12/Vulkan等のネイティブライブラリが同梱されており、
/// Whisper.net自身が起動時にハードウェアを自動検出して最適なものをロードする(GPUベンダーを問わない)。
/// </summary>
public sealed class WhisperNetProvider : ITranscriptionProvider, IDisposable
{
    private const int RequiredSampleRate = 16000;

    private readonly WhisperFactory _factory;

    public WhisperNetProvider(string modelPath)
    {
        _factory = WhisperFactory.FromPath(modelPath);
        RuntimeInfo = DescribeLoadedRuntime();
    }

    /// <summary>実際にロードされたWhisper.netのネイティブ実行バックエンド(CPU/GPU)を人間可読な文字列で表す。</summary>
    public string RuntimeInfo { get; }

    private static string DescribeLoadedRuntime() => RuntimeOptions.LoadedLibrary switch
    {
        RuntimeLibrary.Cuda => "GPU (NVIDIA CUDA)",
        RuntimeLibrary.Cuda12 => "GPU (NVIDIA CUDA 12)",
        RuntimeLibrary.Vulkan => "GPU (Vulkan)",
        RuntimeLibrary.CoreML => "GPU (Apple CoreML)",
        RuntimeLibrary.OpenVino => "GPU/NPU (Intel OpenVINO)",
        RuntimeLibrary.Cpu => "CPU",
        RuntimeLibrary.CpuNoAvx => "CPU (NoAVX)",
        _ => Strings.Get("whisper.runtime_unknown"),
    };

    public async Task<TranscriptionResult> TranscribeAsync(Stream wavStream, string language = "auto", CancellationToken cancellationToken = default)
    {
        using Stream resampled = Resample16k(wavStream);

        using WhisperProcessor processor = _factory.CreateBuilder()
            .WithLanguage(language)
            .Build();

        var segments = new List<TranscriptionSegment>();
        var textBuilder = new StringBuilder();
        string? detectedLanguage = null;

        await foreach (SegmentData segment in processor.ProcessAsync(resampled, cancellationToken).ConfigureAwait(false))
        {
            segments.Add(new TranscriptionSegment(segment.Start, segment.End, segment.Text));
            textBuilder.Append(segment.Text);
            detectedLanguage ??= segment.Language;
        }

        return new TranscriptionResult(textBuilder.ToString().Trim(), detectedLanguage, segments);
    }

    private static Stream Resample16k(Stream wavStream)
    {
        using var reader = new WaveFileReader(wavStream);
        var resampler = new WdlResamplingSampleProvider(reader.ToSampleProvider(), RequiredSampleRate);

        var outputStream = new MemoryStream();
        WaveFileWriter.WriteWavFileToStream(outputStream, resampler.ToWaveProvider16());
        outputStream.Position = 0;
        return outputStream;
    }

    public void Dispose() => _factory.Dispose();
}
