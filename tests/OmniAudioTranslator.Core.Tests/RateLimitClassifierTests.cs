using System.Net;
using OmniAudioTranslator.Core.Translation;

namespace OmniAudioTranslator.Core.Tests;

public class RateLimitClassifierTests
{
    [Fact]
    public void TooManyRequestsStatusCode_IsRateLimit()
    {
        Assert.True(RateLimitClassifier.IsRateLimitError(HttpStatusCode.TooManyRequests, null));
    }

    [Theory]
    [InlineData("""{"error":{"status":"RESOURCE_EXHAUSTED"}}""")]
    [InlineData("Rate limit exceeded, please retry later")]
    [InlineData("You have exceeded your quota")]
    public void KnownErrorMarkers_AreClassifiedAsRateLimit(string body)
    {
        Assert.True(RateLimitClassifier.IsRateLimitError(HttpStatusCode.BadRequest, body));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Invalid target language")]
    public void UnrelatedErrors_AreNotRateLimit(string? body)
    {
        Assert.False(RateLimitClassifier.IsRateLimitError(HttpStatusCode.BadRequest, body));
    }
}
