namespace OmniAudioTranslator.Core.Capture;

/// <summary>PCMバッファを標準的な44バイトヘッダのWAVコンテナへ変換する。</summary>
public static class WavEncoder
{
    public static byte[] Encode(ReadOnlySpan<byte> pcm, int sampleRate, int channels, int bitsPerSample)
    {
        int blockAlign = channels * bitsPerSample / 8;
        int byteRate = sampleRate * blockAlign;

        using var stream = new MemoryStream(44 + pcm.Length);
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8);
        writer.Write(36 + pcm.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1); // WAVE_FORMAT_PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);

        writer.Flush();
        return stream.ToArray();
    }
}
