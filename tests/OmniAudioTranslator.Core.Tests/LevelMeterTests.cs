using OmniAudioTranslator.Core.Capture;

namespace OmniAudioTranslator.Core.Tests;

public class LevelMeterTests
{
    [Fact]
    public void Silence_DoesNotTriggerSpeech()
    {
        var meter = new LevelMeter(thresholdPercent: 10);

        meter.ProcessSamples(new byte[100]);

        Assert.False(meter.HadSpeech);
        Assert.Equal(0, meter.MaxLevel);
    }

    [Fact]
    public void FullScaleSample_TriggersSpeechAndReaches100Percent()
    {
        var meter = new LevelMeter(thresholdPercent: 10);
        byte[] buffer = ToLittleEndianBytes(short.MaxValue);

        meter.ProcessSamples(buffer);

        Assert.True(meter.HadSpeech);
        Assert.Equal(100, meter.MaxLevel);
    }

    [Fact]
    public void SampleBelowThreshold_DoesNotTriggerSpeech()
    {
        var meter = new LevelMeter(thresholdPercent: 50);
        // 約10%相当の振幅
        byte[] buffer = ToLittleEndianBytes((short)(short.MaxValue * 0.1));

        meter.ProcessSamples(buffer);

        Assert.False(meter.HadSpeech);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var meter = new LevelMeter();
        meter.ProcessSamples(ToLittleEndianBytes(short.MaxValue));

        meter.Reset();

        Assert.False(meter.HadSpeech);
        Assert.Equal(0, meter.MaxLevel);
    }

    private static byte[] ToLittleEndianBytes(short sample) =>
        [(byte)(sample & 0xFF), (byte)((sample >> 8) & 0xFF)];
}
