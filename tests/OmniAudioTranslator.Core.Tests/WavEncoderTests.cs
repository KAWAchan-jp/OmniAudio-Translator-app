using System.Text;
using OmniAudioTranslator.Core.Capture;

namespace OmniAudioTranslator.Core.Tests;

public class WavEncoderTests
{
    [Fact]
    public void Encode_ProducesValidRiffHeader()
    {
        byte[] pcm = new byte[100];
        byte[] wav = WavEncoder.Encode(pcm, sampleRate: 16000, channels: 1, bitsPerSample: 16);

        Assert.Equal(44 + pcm.Length, wav.Length);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(wav, 12, 4));
        Assert.Equal("data", Encoding.ASCII.GetString(wav, 36, 4));
    }

    [Fact]
    public void Encode_WritesCorrectFormatFields()
    {
        byte[] pcm = new byte[200];
        byte[] wav = WavEncoder.Encode(pcm, sampleRate: 44100, channels: 2, bitsPerSample: 16);

        short audioFormat = BitConverter.ToInt16(wav, 20);
        short channels = BitConverter.ToInt16(wav, 22);
        int sampleRate = BitConverter.ToInt32(wav, 24);
        int byteRate = BitConverter.ToInt32(wav, 28);
        short blockAlign = BitConverter.ToInt16(wav, 32);
        short bitsPerSample = BitConverter.ToInt16(wav, 34);
        int dataSize = BitConverter.ToInt32(wav, 40);

        Assert.Equal(1, audioFormat);
        Assert.Equal(2, channels);
        Assert.Equal(44100, sampleRate);
        Assert.Equal(44100 * 2 * 2, byteRate);
        Assert.Equal(4, blockAlign);
        Assert.Equal(16, bitsPerSample);
        Assert.Equal(200, dataSize);
    }
}
