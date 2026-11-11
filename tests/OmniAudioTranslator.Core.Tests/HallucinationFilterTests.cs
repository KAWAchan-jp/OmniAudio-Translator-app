using OmniAudioTranslator.Core.Transcription;

namespace OmniAudioTranslator.Core.Tests;

public class HallucinationFilterTests
{
    [Theory]
    [InlineData("ご視聴ありがとうございました")]
    [InlineData("ご視聴ありがとうございました。")]
    [InlineData("ご視聴ありがとうございます")]
    [InlineData("チャンネル登録よろしくお願いします")]
    [InlineData("Thank you for watching.")]
    [InlineData("Thanks for watching!")]
    [InlineData("Please subscribe.")]
    public void CannedPhrase_IsDetectedAsHallucination(string text)
    {
        Assert.True(HallucinationFilter.IsLikelyHallucination(text));
    }

    [Theory]
    [InlineData("今日は良い天気ですね。")]
    [InlineData("This is a normal sentence about the weather today.")]
    [InlineData("")]
    public void NormalText_IsNotHallucination(string text)
    {
        Assert.False(HallucinationFilter.IsLikelyHallucination(text));
    }

    [Fact]
    public void RepeatedNearIdenticalSentences_IsDetectedAsLoop()
    {
        string text = "これはテストです。これはテストです。これはテストです。";

        Assert.True(HallucinationFilter.HasRepeatedSentenceLoop(text));
        Assert.True(HallucinationFilter.IsLikelyHallucination(text));
    }

    [Fact]
    public void TwoSentencesOnly_IsNotEnoughToBeLoop()
    {
        // 文が3つ未満の場合は反復判定の対象外
        string text = "これはテストです。これはテストです。";

        Assert.False(HallucinationFilter.HasRepeatedSentenceLoop(text));
    }

    [Fact]
    public void DistinctSentences_IsNotLoop()
    {
        string text = "今日は晴れです。明日は雨かもしれません。来週は台風が来るそうです。";

        Assert.False(HallucinationFilter.HasRepeatedSentenceLoop(text));
    }

    [Theory]
    [InlineData("abcde", "abcde", 1.0)]
    [InlineData("", "", 1.0)]
    [InlineData("abc", "xyz", 0.0)]
    [InlineData("abcd", "abzz", 0.5)]
    public void SentenceSimilarity_MatchesExpected(string a, string b, double expected)
    {
        Assert.Equal(expected, HallucinationFilter.SentenceSimilarity(a, b), precision: 5);
    }
}
