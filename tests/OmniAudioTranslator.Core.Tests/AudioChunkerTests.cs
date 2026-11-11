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
        chunker.ChunkReady += (_, _) => skippedTcs.TrySetException(new InvalidOperationException("無音チャンクなのにChunkReadyが発火した"));

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
        chunker.ChunkReady += (wav, level) => readyTcs.TrySetResult((wav, level));

        chunker.Start();
        chunker.AddSamples(BuildLoudSamples(sampleCount: 800));

        (byte[] wav, int level) = await readyTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(level >= 10);
        Assert.True(wav.Length > 44);
        Assert.Equal((byte)'R', wav[0]);
        Assert.Equal((byte)'I', wav[1]);
        Assert.Equal((byte)'F', wav[2]);
        Assert.Equal((byte)'F', wav[3]);
    }

    private static byte[] BuildLoudSamples(int sampleCount)
    {
        var buffer = new byte[sampleCount * 2];
        const short amplitude = 20000;
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = i % 2 == 0 ? amplitude : (short)-amplitude;
            buffer[i * 2] = (byte)(sample & 0xFF);
            buffer[(i * 2) + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return buffer;
    }
}
