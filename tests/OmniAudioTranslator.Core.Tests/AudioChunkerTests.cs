using OmniAudioTranslator.Core.Capture;

namespace OmniAudioTranslator.Core.Tests;

public class AudioChunkerTests
{
    [Fact]
    public async Task SilentChunk_IsSkipped()
    {
        using var chunker = new AudioChunker(sampleRate: 16000, channels: 1, bitsPerSample: 16, chunkMillis: 50, vadThresholdPercent: 10);
        var skippedTcs = new TaskCompletionSource<int>();
        chunker.ChunkSkipped += level => skippedTcs.TrySetResult(level);
        chunker.ChunkReady += (_, _, _) => skippedTcs.TrySetException(new InvalidOperationException("無音チャンクなのにChunkReadyが発火した"));

        chunker.Start();
        chunker.AddSamples(new byte[3200]); // 全て無音

        int level = await skippedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, level);
    }

    [Fact]
    public async Task LoudChunk_ProducesWavChunk()
    {
        using var chunker = new AudioChunker(sampleRate: 16000, channels: 1, bitsPerSample: 16, chunkMillis: 50, vadThresholdPercent: 10);
        var readyTcs = new TaskCompletionSource<(byte[] Wav, int Level)>();
        chunker.ChunkReady += (wav, level, _) => readyTcs.TrySetResult((wav, level));

        chunker.Start();
        chunker.AddSamples(BuildConstantSamples(sampleCount: 800, amplitude: 20000));

        (byte[] wav, int level) = await readyTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(level >= 10);
        Assert.True(wav.Length > 44);
        Assert.Equal((byte)'R', wav[0]);
        Assert.Equal((byte)'I', wav[1]);
        Assert.Equal((byte)'F', wav[2]);
        Assert.Equal((byte)'F', wav[3]);
    }

    [Fact]
    public async Task QuietChunk_BelowDefaultThreshold_IsStillRecognizedAndAmplified()
    {
        // 既定の閾値(2%)をわずかに超える小さい音声。以前の固定10%閾値では捨てられていたケース。
        using var chunker = new AudioChunker(sampleRate: 16000, channels: 1, bitsPerSample: 16, chunkMillis: 50);
        var readyTcs = new TaskCompletionSource<(int Level, double GainDb)>();
        chunker.ChunkReady += (_, level, gainDb) => readyTcs.TrySetResult((level, gainDb));
        chunker.ChunkSkipped += _ => readyTcs.TrySetException(new InvalidOperationException("小さいが発話ありのチャンクが無音扱いされた"));

        chunker.Start();
        chunker.AddSamples(BuildConstantSamples(sampleCount: 800, amplitude: (short)(short.MaxValue * 0.04)));

        (int level, double gainDb) = await readyTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.InRange(level, 3, 5);
        Assert.True(gainDb > 20);
    }

    [Fact]
    public async Task LoudChunk_GainIsZero()
    {
        using var chunker = new AudioChunker(sampleRate: 16000, channels: 1, bitsPerSample: 16, chunkMillis: 50);
        var readyTcs = new TaskCompletionSource<double>();
        chunker.ChunkReady += (_, _, gainDb) => readyTcs.TrySetResult(gainDb);

        chunker.Start();
        chunker.AddSamples(BuildConstantSamples(sampleCount: 800, amplitude: (short)(short.MaxValue * 0.95)));

        double gainDb = await readyTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, gainDb);
    }

    [Fact]
    public async Task SetVadThresholdPercent_WhileRunning_TakesEffectForNextChunk()
    {
        // 起動時は既定の2%。ピーク1%相当の音声は一度目は無音として捨てられる。
        using var chunker = new AudioChunker(sampleRate: 16000, channels: 1, bitsPerSample: 16, chunkMillis: 50);
        var firstResult = new TaskCompletionSource<bool>();
        chunker.ChunkSkipped += _ => firstResult.TrySetResult(true);
        chunker.ChunkReady += (_, _, _) => firstResult.TrySetResult(false);

        chunker.Start();
        chunker.AddSamples(BuildConstantSamples(sampleCount: 800, amplitude: (short)(short.MaxValue * 0.01)));

        Assert.True(await firstResult.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        // 実行中に1%へ下げると、次のチャンクから発話として扱われる(停止・再開始は不要)。
        chunker.SetVadThresholdPercent(1);
        var secondResult = new TaskCompletionSource<double>();
        chunker.ChunkReady += (_, _, gainDb) => secondResult.TrySetResult(gainDb);
        chunker.ChunkSkipped += _ => secondResult.TrySetException(new InvalidOperationException("しきい値を下げたのに無音扱いされた"));

        chunker.AddSamples(BuildConstantSamples(sampleCount: 800, amplitude: (short)(short.MaxValue * 0.01)));

        double gainDb = await secondResult.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(gainDb > 20);
    }

    private static byte[] BuildConstantSamples(int sampleCount, short amplitude)
    {
        var buffer = new byte[sampleCount * 2];
        for (int i = 0; i < sampleCount; i++)
        {
            buffer[i * 2] = (byte)(amplitude & 0xFF);
            buffer[(i * 2) + 1] = (byte)((amplitude >> 8) & 0xFF);
        }

        return buffer;
    }
}
