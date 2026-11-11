namespace OmniAudioTranslator.Core.Capture;

/// <summary>
/// PCMストリームを一定間隔(既定6秒)ごとにチャンク化し、無音チャンクを破棄してWAVとして通知する。
/// Stream-Speech-Layerの offscreen.js の startRecorderCycle()/resetChunkState() を、
/// MediaRecorderの開始/停止サイクルから<see cref="Timer"/>ベースのバッファスワップへ置き換えたもの。
/// </summary>
public sealed class AudioChunker : IDisposable
{
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly int _bitsPerSample;
    private readonly int _chunkMillis;
    private readonly LevelMeter _levelMeter;
    private readonly object _lock = new();
    private readonly List<byte> _buffer = [];
    private Timer? _timer;

    public AudioChunker(int sampleRate, int channels, int bitsPerSample, int chunkMillis = 6000, int vadThresholdPercent = 10)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _bitsPerSample = bitsPerSample;
        _chunkMillis = chunkMillis;
        _levelMeter = new LevelMeter(vadThresholdPercent);
    }

    /// <summary>発話が検出されたチャンクが確定した際に発火する(wavBytes, peakLevel)。</summary>
    public event Action<byte[], int>? ChunkReady;

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

        byte[] wav = WavEncoder.Encode(pcm, _sampleRate, _channels, _bitsPerSample);
        ChunkReady?.Invoke(wav, peakLevel);
    }

    public void Dispose() => Stop();
}
