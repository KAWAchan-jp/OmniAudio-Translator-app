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
    public void RaisingThreshold_AfterProcessing_AppliesToNextBatch()
    {
        var meter = new LevelMeter(thresholdPercent: 2);
        byte[] quiet = ToLittleEndianBytes((short)(short.MaxValue * 0.05));

        meter.ProcessSamples(quiet);
        Assert.True(meter.HadSpeech);

        // 実行中に閾値を上げても、次に処理するサンプルから適用される。
        meter.ThresholdPercent = 20;
        meter.Reset();
        meter.ProcessSamples(quiet);

        Assert.False(meter.HadSpeech);
    }

    [Fact]
    public void ThresholdPercent_IsClampedToValidRange()
    {
        var meter = new LevelMeter();

        meter.ThresholdPercent = 0;
        Assert.Equal(1, meter.ThresholdPercent);

        meter.ThresholdPercent = 500;
        Assert.Equal(100, meter.ThresholdPercent);
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
