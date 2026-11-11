using Whisper.net.Ggml;

namespace OmniAudioTranslator.Core.Transcription;

/// <summary>
/// Whisper.net用GGMLモデルのライフサイクル管理。初回起動時にHugging Faceから自動ダウンロードし、
/// 以降は<c>Models/</c>ディレクトリのキャッシュを利用する。ユーザーの手動起動は一切不要。
/// </summary>
public sealed class WhisperModelManager
{
    private readonly string _modelsDirectory;

    public WhisperModelManager(string? modelsDirectory = null)
    {
        _modelsDirectory = modelsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Models");
    }

    public async Task<string> EnsureModelAsync(GgmlType ggmlType = GgmlType.Small, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_modelsDirectory);
        string path = Path.Combine(_modelsDirectory, $"ggml-{ToFileSuffix(ggmlType)}.bin");

        if (!File.Exists(path))
        {
            // ダウンロード中の中断で壊れたファイルを残さないよう、一時ファイルに書いてからリネームする。
            string tempPath = path + ".download";
            using (Stream modelStream = await Whisper.net.Ggml.WhisperGgmlDownloader.Default
                .GetGgmlModelAsync(ggmlType, cancellationToken: cancellationToken)
                .ConfigureAwait(false))
            using (FileStream fileStream = File.Create(tempPath))
            {
                await modelStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, path);
        }

        return path;
    }

    private static string ToFileSuffix(GgmlType type) => type switch
    {
        GgmlType.Tiny => "tiny",
        GgmlType.TinyEn => "tiny.en",
        GgmlType.Base => "base",
        GgmlType.BaseEn => "base.en",
        GgmlType.Small => "small",
        GgmlType.SmallEn => "small.en",
        GgmlType.Medium => "medium",
        GgmlType.MediumEn => "medium.en",
        GgmlType.LargeV1 => "large-v1",
        GgmlType.LargeV2 => "large-v2",
        GgmlType.LargeV3 => "large-v3",
        GgmlType.LargeV3Turbo => "large-v3-turbo",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
