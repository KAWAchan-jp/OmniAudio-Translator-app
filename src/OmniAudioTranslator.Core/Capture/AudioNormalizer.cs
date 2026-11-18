namespace OmniAudioTranslator.Core.Capture;

/// <summary>ピーク正規化の結果。変換後のPCMと、増幅前後のピーク(%)・増幅量(dB)を保持する。</summary>
public readonly record struct AudioNormalizationResult(byte[] Pcm, int InputPeakPercent, int OutputPeakPercent, double GainDb);

/// <summary>
/// 16bit PCMのピークを目標値まで持ち上げるピーク正規化。
/// プロセスループバックはアプリ個別音量より後・出力デバイスのマスター音量より前を拾うため、
/// 配信側の音量が小さいと取得PCMの振幅も小さいまま届く。ASR(Whisper)は振幅が小さいと精度が落ちるため、
/// チャンク確定時にこのクラスで増幅してから認識へ渡す。
/// 減衰は行わない(既に十分大きい音声はそのまま)。増幅率には上限を設け、無音に近いデータを
/// 巨大なゲインで持ち上げてノイズだけを増幅しないようにする。
/// </summary>
public static class AudioNormalizer
{
    /// <summary>正規化後のピーク目標値(% of full scale)。</summary>
    public const int DefaultTargetPeakPercent = 90;

    /// <summary>増幅率の上限(dB)。これ以上小さい音声は増幅しきらずに諦める。</summary>
    public const double DefaultMaxGainDb = 30;

    /// <summary>発話ありと判定する既定のピーク閾値(%)。正規化と併用する前提で低めにしている。</summary>
    public const int DefaultVadThresholdPercent = 2;

    /// <summary>PCMを目標ピークまで増幅する。無音・既に大きい音声はそのまま返す。</summary>
    public static AudioNormalizationResult Normalize(
        ReadOnlySpan<byte> pcm,
        int targetPeakPercent = DefaultTargetPeakPercent,
        double maxGainDb = DefaultMaxGainDb)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(targetPeakPercent, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(targetPeakPercent, 100);

        int peak = FindPeak(pcm);
        int inputPercent = ToPercent(peak);

        if (peak == 0)
        {
            return new AudioNormalizationResult(pcm.ToArray(), 0, 0, 0);
        }

        double desiredGain = short.MaxValue * (targetPeakPercent / 100.0) / peak;
        double gain = Math.Min(desiredGain, Math.Pow(10, maxGainDb / 20.0));

        if (gain <= 1.0)
        {
            return new AudioNormalizationResult(pcm.ToArray(), inputPercent, inputPercent, 0);
        }

        byte[] output = new byte[pcm.Length];
        int outputPeak = 0;

        for (int i = 0; i + 1 < pcm.Length; i += 2)
        {
            short sample = (short)(pcm[i] | (pcm[i + 1] << 8));
            int scaled = (int)Math.Round(sample * gain);
            short clamped = (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
            output[i] = (byte)(clamped & 0xFF);
            output[i + 1] = (byte)((clamped >> 8) & 0xFF);
            outputPeak = Math.Max(outputPeak, Math.Abs((int)clamped));
        }

        // 端数バイト(16bit境界に満たない末尾)はそのまま複製する。
        if (pcm.Length % 2 != 0)
        {
            output[^1] = pcm[^1];
        }

        return new AudioNormalizationResult(output, inputPercent, ToPercent(outputPeak), 20 * Math.Log10(gain));
    }

    /// <summary>16bit PCMバッファの絶対値ピークを返す。</summary>
    public static int FindPeak(ReadOnlySpan<byte> pcm)
    {
        int peak = 0;
        for (int i = 0; i + 1 < pcm.Length; i += 2)
        {
            short sample = (short)(pcm[i] | (pcm[i + 1] << 8));
            int abs = Math.Abs((int)sample);
            if (abs > peak)
            {
                peak = abs;
            }
        }

        return peak;
    }

    private static int ToPercent(int peak) => (int)Math.Round(peak / (double)short.MaxValue * 100);
}
