using ContractReview.Application.Features.Claims.Evaluation;
using ContractReview.Domain.Claims;

namespace ContractReview.UnitTests.Application;

public class LlmClaimReviewTests
{
    private static LlmClaimReview Valid() => new(true, true, 50, "Because.", ["a"], ["b"]);

    [Fact]
    public void FindViolation_is_null_for_a_valid_review() => Assert.Null(Valid().FindViolation());

    [Theory]
    [InlineData(ClaimRules.MinConfidence, true)]
    [InlineData(ClaimRules.MaxConfidence, true)]
    [InlineData(ClaimRules.MinConfidence - 1, false)]
    [InlineData(ClaimRules.MaxConfidence + 1, false)]
    public void FindViolation_checks_confidence_bounds(int confidence, bool valid) =>
        Assert.Equal(valid, (Valid() with { Confidence = confidence }).FindViolation() is null);

    [Theory]
    [InlineData(1, true)]
    [InlineData(ClaimRules.MaxReviewExplanationLength, true)]
    [InlineData(ClaimRules.MaxReviewExplanationLength + 1, false)]
    [InlineData(0, false)]
    public void FindViolation_checks_explanation_length(int length, bool valid) =>
        Assert.Equal(valid, (Valid() with { AccuracyExplanation = new string('a', length) }).FindViolation() is null);

    [Fact]
    public void FindViolation_rejects_whitespace_explanation() =>
        Assert.NotNull((Valid() with { AccuracyExplanation = "   " }).FindViolation());

    [Theory]
    [InlineData(ClaimRules.MaxReviewItems, true)]
    [InlineData(ClaimRules.MaxReviewItems + 1, false)]
    public void FindViolation_checks_item_count_in_both_lists(int count, bool valid)
    {
        var items = Enumerable.Repeat("x", count).ToList();
        Assert.Equal(valid, (Valid() with { CoveredItems = items }).FindViolation() is null);
        Assert.Equal(valid, (Valid() with { NotCoveredItems = items }).FindViolation() is null);
    }

    [Theory]
    [InlineData(ClaimRules.MaxItemLength, true)]
    [InlineData(ClaimRules.MaxItemLength + 1, false)]
    [InlineData(0, false)]
    public void FindViolation_checks_item_length(int length, bool valid)
    {
        var items = new[] { new string('x', length) };
        Assert.Equal(valid, (Valid() with { CoveredItems = items }).FindViolation() is null);
        Assert.Equal(valid, (Valid() with { NotCoveredItems = items }).FindViolation() is null);
    }
}
