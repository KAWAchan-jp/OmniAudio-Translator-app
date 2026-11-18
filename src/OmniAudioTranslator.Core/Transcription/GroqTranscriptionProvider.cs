using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OmniAudioTranslator.Core.Localization;
using OmniAudioTranslator.Core.Translation;

namespace OmniAudioTranslator.Core.Transcription;

/// <summary>
/// GroqのOpenAI互換音声認識API(<c>/openai/v1/audio/transcriptions</c>)を使うクラウドASR。
/// 内蔵Whisper.netと違いGPUを使わないため、GPUが弱い/使いたくない環境向けの選択肢として提供する。
/// 入力は<c>AudioChunker</c>が生成したWAVをそのまま送る(Groq側が内部でリサンプルするため、こちらでの変換は不要)。
/// </summary>
public sealed class GroqTranscriptionProvider : ITranscriptionProvider, IDisposable
{
    public const string DefaultModel = "whisper-large-v3-turbo";

    /// <summary>UIで選択できるGroqの音声認識モデル。</summary>
    public static readonly IReadOnlyList<string> SupportedModels =
    [
        DefaultModel,
        "whisper-large-v3",
    ];

    private const string Endpoint = "https://api.groq.com/openai/v1/audio/transcriptions";

    // エラー本文が巨大でもUI・ログを圧迫しないように切り詰める。
    private const int MaxErrorBodyLength = 2000;

    private readonly string _apiKey;
    private readonly string _model;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public GroqTranscriptionProvider(string apiKey, string model = DefaultModel, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
    }

    public string Model => _model;

    public async Task<TranscriptionResult> TranscribeAsync(Stream wavStream, string language = "auto", CancellationToken cancellationToken = default)
    {
        byte[] audio = await ReadAllBytesAsync(wavStream, cancellationToken).ConfigureAwait(false);

        // MultipartFormDataContentは追加した子コンテンツもまとめて破棄する。
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(audio);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(fileContent, "file", "audio.wav");
        form.Add(new StringContent(_model, Encoding.UTF8), "model");
        form.Add(new StringContent("verbose_json", Encoding.UTF8), "response_format");
        form.Add(new StringContent("0", Encoding.UTF8), "temperature");

        // "auto"はGroqへ送らない(省略すると自動判定になる)。
        if (!string.IsNullOrWhiteSpace(language) && !string.Equals(language, "auto", StringComparison.OrdinalIgnoreCase))
        {
            form.Add(new StringContent(language, Encoding.UTF8), "language");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string truncated = Truncate(body);
                throw new TranscriptionProviderException(
                    Strings.Format("transcription.groq_error", (int)response.StatusCode, truncated),
                    response.StatusCode,
                    // レート制限判定は翻訳側と共通のロジックを流用する(同一アセンブリ)。
                    RateLimitClassifier.IsRateLimitError(response.StatusCode, body),
                    truncated);
            }

            return Parse(body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 呼び出し元(停止処理)によるキャンセルはそのまま伝える。
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new TranscriptionProviderException(
                Strings.Format("transcription.groq_network_error", ex.Message));
        }
        catch (OperationCanceledException ex)
        {
            // キャンセル指定のないOperationCanceledExceptionはHttpClientのタイムアウト。
            throw new TranscriptionProviderException(
                Strings.Format("transcription.groq_network_error", ex.Message));
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private static TranscriptionResult Parse(string body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;

            string text = root.TryGetProperty("text", out JsonElement textElement) && textElement.ValueKind == JsonValueKind.String
                ? textElement.GetString() ?? string.Empty
                : string.Empty;

            string? language = root.TryGetProperty("language", out JsonElement languageElement) && languageElement.ValueKind == JsonValueKind.String
                ? languageElement.GetString()
                : null;

            var segments = new List<TranscriptionSegment>();
            if (root.TryGetProperty("segments", out JsonElement segmentsElement) && segmentsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement segment in segmentsElement.EnumerateArray())
                {
                    segments.Add(new TranscriptionSegment(
                        TimeSpan.FromSeconds(GetDouble(segment, "start")),
                        TimeSpan.FromSeconds(GetDouble(segment, "end")),
                        GetString(segment, "text")));
                }
            }

            return new TranscriptionResult(text.Trim(), language, segments);
        }
        catch (JsonException ex)
        {
            throw new TranscriptionProviderException(
                Strings.Format("transcription.groq_invalid_response", ex.Message),
                null,
                false,
                Truncate(body));
        }
    }

    private static double GetDouble(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.TryGetDouble(out double result) ? result : 0;

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= MaxErrorBodyLength ? value : value[..MaxErrorBodyLength] + "…";
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
