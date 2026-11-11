namespace OmniAudioTranslator.Core.Capture;

/// <summary>
/// 簡易VAD(発話区間検出)。16bit PCMバッファの絶対値ピークをパーセント換算し、
/// 閾値(既定10%)を超えたら発話ありと判定する。
/// Stream-Speech-Layerの offscreen.js の startLevelMeter() (Web Audio AnalyserNodeによる
/// 周波数ドメインのピーク検出)を、時間ドメインの振幅ピーク検出に置き換えたもの
/// (実装計画の移植方針: AnalyserNodeピーク検出→NAudio/生PCMピーク検出への置換)。
/// </summary>
public sealed class LevelMeter(int thresholdPercent = 10)
{
    /// <summary>直近のリセット以降に観測した最大レベル(0-100)。</summary>
    public int MaxLevel { get; private set; }

    /// <summary>直近のリセット以降に閾値以上のレベルを観測したか。</summary>
    public bool HadSpeech { get; private set; }

    public void Reset()
    {
        MaxLevel = 0;
        HadSpeech = false;
    }

    /// <summary>リトルエンディアンの16bit PCMバッファを処理し、内部状態を更新する。</summary>
    public void ProcessSamples(ReadOnlySpan<byte> buffer)
    {
        int peakAbs = 0;
        for (int i = 0; i + 1 < buffer.Length; i += 2)
        {
            short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
            int abs = Math.Abs((int)sample);
            if (abs > peakAbs)
            {
                peakAbs = abs;
            }
        }

        int level = (int)Math.Round(peakAbs / (double)short.MaxValue * 100);
        if (level > MaxLevel)
        {
            MaxLevel = level;
        }

        if (level >= thresholdPercent)
        {
            HadSpeech = true;
        }
    }
}
