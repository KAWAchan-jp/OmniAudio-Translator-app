using OmniAudioTranslator.Core.Capture;

namespace OmniAudioTranslator.Core.Tests;

public class AudioNormalizerTests
{
    [Fact]
    public void QuietSpeech_IsAmplifiedTowardTargetPeak()
    {
        // ピーク約10%相当の小さい音声
        byte[] pcm = BuildConstantSamples(sampleCount: 100, (short)(short.MaxValue * 0.1));

        AudioNormalizationResult result = AudioNormalizer.Normalize(pcm);

        Assert.Equal(10, result.InputPeakPercent);
        Assert.InRange(result.OutputPeakPercent, 88, 90);
        Assert.True(result.GainDb > 15);
    }

    [Fact]
    public void AlreadyLoudAudio_IsNotAmplifiedOrAttenuated()
    {
        byte[] pcm = BuildConstantSamples(sampleCount: 100, (short)(short.MaxValue * 0.95));

        AudioNormalizationResult result = AudioNormalizer.Normalize(pcm);

        Assert.Equal(0, result.GainDb);
        Assert.Equal(result.InputPeakPercent, result.OutputPeakPercent);
    }

    [Fact]
    public void Silence_IsReturnedUnchanged()
    {
        byte[] pcm = new byte[200];

        AudioNormalizationResult result = AudioNormalizer.Normalize(pcm);

        Assert.Equal(0, result.GainDb);
        Assert.Equal(0, result.OutputPeakPercent);
        Assert.Equal(pcm, result.Pcm);
    }

    [Fact]
    public void ExtremelyQuietAudio_GainIsCappedAtMaxGain()
    {
        // ピーク1%未満。上限30dB(+約31.6倍)では目標90%まで届かないため、頭打ちになる。
        byte[] pcm = BuildConstantSamples(sampleCount: 100, (short)(short.MaxValue * 0.005));

        AudioNormalizationResult result = AudioNormalizer.Normalize(pcm);

        Assert.InRange(result.GainDb, 29.9, 30.0);
        Assert.InRange(result.OutputPeakPercent, 15, 16);
    }

    [Fact]
    public void AmplifiedSamples_DoNotOverflow()
    {
        byte[] pcm = BuildConstantSamples(sampleCount: 100, (short)(short.MaxValue * 0.5));

        AudioNormalizationResult result = AudioNormalizer.Normalize(pcm, targetPeakPercent: 100);

        Assert.True(result.OutputPeakPercent <= 100);
        Assert.Equal(pcm.Length, result.Pcm.Length);
    }

    [Fact]
    public void AudioLength_IsPreserved()
    {
        byte[] pcm = BuildConstantSamples(sampleCount: 333, 1000);

        AudioNormalizationResult result = AudioNormalizer.Normalize(pcm);

        Assert.Equal(pcm.Length, result.Pcm.Length);
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
