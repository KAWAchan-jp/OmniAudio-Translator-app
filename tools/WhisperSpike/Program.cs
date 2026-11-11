using OmniAudioTranslator.Core.Transcription;

namespace WhisperSpike;

/// <summary>
/// Phase 2検証用の使い捨てコンソールツール。指定したWAVファイルをWhisper.netで認識し、結果を表示する。
/// 詳細: docs/feature-whisper-net-transcription.md
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length == 0 || !File.Exists(args[0]))
        {
            Console.WriteLine("使い方: WhisperSpike <wavファイルパス> [言語=auto]");
            return 1;
        }

        string wavPath = args[0];
        string language = args.Length > 1 ? args[1] : "auto";

        var modelManager = new WhisperModelManager();
        Console.WriteLine("モデルを確認中（未取得の場合は初回のみ自動ダウンロード）...");
        string modelPath = await modelManager.EnsureModelAsync();
        Console.WriteLine($"モデル: {modelPath}");

        using var provider = new WhisperNetProvider(modelPath);

        Console.WriteLine($"認識中: {wavPath} (language={language})");
        await using FileStream wavStream = File.OpenRead(wavPath);
        TranscriptionResult result = await provider.TranscribeAsync(wavStream, language);

        Console.WriteLine();
        Console.WriteLine($"検出言語: {result.Language}");
        Console.WriteLine($"認識結果: {result.Text}");
        Console.WriteLine();
        foreach (TranscriptionSegment segment in result.Segments)
        {
            Console.WriteLine($"  {segment.Start}->{segment.End}: {segment.Text}");
        }

        return 0;
    }
}
