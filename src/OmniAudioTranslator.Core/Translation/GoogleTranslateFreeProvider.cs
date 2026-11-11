using System.Text;
using System.Text.Json;
using OmniAudioTranslator.Core.Localization;

namespace OmniAudioTranslator.Core.Translation;

/// <summary>
/// Google翻訳の無料・非公式エンドポイント(translate.googleapis.com/translate_a/single)による翻訳。
/// APIキー・課金設定は不要。前身のStream-Speech-Layer(background.js)が使っていたのと同じ方式の移植。
/// 非公式・無保証のエンドポイントであり、大量アクセスやGoogle側の仕様変更で動作しなくなる可能性がある点に注意。
/// </summary>
public sealed class GoogleTranslateFreeProvider : ITranslationProvider, IDisposable
{
    private const string Endpoint = "https://translate.googleapis.com/translate_a/single";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public GoogleTranslateFreeProvider(HttpClient? httpClient = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken cancellationToken = default)
    {
        string url = $"{Endpoint}?client=gtx&sl=auto&tl={Uri.EscapeDataString(targetLanguage)}&dt=t&q={Uri.EscapeDataString(text)}";

        using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new TranslationProviderException(
                Strings.Format("translation.google_free_error", (int)response.StatusCode, body),
                RateLimitClassifier.IsRateLimitError(response.StatusCode, body));
        }

        // レスポンスは正規のJSONオブジェクトではなく、[[[訳文, 原文, ...], [訳文, 原文, ...], ...], ...]という
        // ネストした配列。長文は複数セグメントに分割されて返るため、各セグメントの訳文(添字0)を連結する。
        using var document = JsonDocument.Parse(body);
        var builder = new StringBuilder();
        foreach (JsonElement segment in document.RootElement[0].EnumerateArray())
        {
            string? chunk = segment[0].GetString();
            if (chunk is not null)
            {
                builder.Append(chunk);
            }
        }

        return builder.ToString();
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
