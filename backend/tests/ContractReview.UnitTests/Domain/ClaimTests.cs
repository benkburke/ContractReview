using ContractReview.Domain.Claims;
using ContractReview.Domain.Errors;

namespace ContractReview.UnitTests.Domain;

public class ClaimTests
{
    private static readonly DateTime Now = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly IReadOnlyList<ClaimCitation> OneCitation = [new ClaimCitation("EXCLUSIONS", "Tires are excluded.")];

    private static Claim Start(string? text = "Is my air conditioner covered?", string? level = null) =>
        Claim.Start(Guid.NewGuid(), text, level, Now);

    private static ClaimEvaluationResult Result(
        ClaimStatus status = ClaimStatus.Covered,
        IReadOnlyList<ClaimCitation>? citations = null,
        int confidence = 80,
        bool needsLevel = false,
        IReadOnlyList<string>? options = null) =>
        ClaimEvaluationResult.Create(
            status, confidence, "Gold", needsLevel, options ?? [], [], [], "Reasoning", null, citations ?? OneCitation, "model");

    [Fact]
    public void Start_trims_text_and_level_and_is_evaluating()
    {
        var claim = Start("  Flat tire on the highway  ", "  Gold ");

        Assert.Equal("Flat tire on the highway", claim.ClaimText);
        Assert.Equal("Gold", claim.CoverageLevelRequested);
        Assert.True(claim.IsEvaluating);
        Assert.Null(claim.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("    ")]
    [InlineData("abcd")]
    public void Start_rejects_short_text(string? text)
    {
        var ex = Assert.Throws<DomainException>(() => Start(text));

        Assert.Equal(ErrorCodes.ClaimTextInvalid, ex.Code);
    }

    [Fact]
    public void Start_rejects_text_over_the_limit()
    {
        var ex = Assert.Throws<DomainException>(() => Start(new string('a', ClaimRules.MaxTextLength + 1)));

        Assert.Equal(ErrorCodes.ClaimTextInvalid, ex.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Start_rejects_empty_level_when_present(string level)
    {
        var ex = Assert.Throws<DomainException>(() => Start(level: level));

        Assert.Equal(ErrorCodes.ClaimCoverageLevelInvalid, ex.Code);
    }

    [Fact]
    public void Start_rejects_level_over_the_limit()
    {
        var ex = Assert.Throws<DomainException>(() => Start(level: new string('a', ClaimRules.MaxCoverageLevelLength + 1)));

        Assert.Equal(ErrorCodes.ClaimCoverageLevelInvalid, ex.Code);
    }

    [Fact]
    public void Complete_stores_the_result_and_marks_evaluated()
    {
        var claim = Start();

        claim.Complete(Result(), Now);

        Assert.Equal(ClaimState.Evaluated, claim.State);
        Assert.Equal(Now, claim.CompletedAtUtc);
        Assert.Equal(ClaimStatus.Covered, claim.Result!.Status);
        Assert.Equal("EXCLUSIONS", claim.Result.Citations[0].SectionHeading);
        Assert.Equal("Reasoning", claim.Result.Reasoning);
    }

    [Theory]
    [InlineData(ClaimStatus.Covered)]
    [InlineData(ClaimStatus.NotCovered)]
    [InlineData(ClaimStatus.Conditional)]
    public void Create_downgrades_a_decisive_status_without_citations(ClaimStatus status)
    {
        var result = Result(status, citations: []);

        Assert.Equal(ClaimStatus.InsufficientInformation, result.Status);
    }

    [Fact]
    public void Create_keeps_insufficient_information_without_citations()
    {
        Assert.Equal(ClaimStatus.InsufficientInformation, Result(ClaimStatus.InsufficientInformation, []).Status);
    }

    [Fact]
    public void Constructor_rejects_a_decisive_status_without_citations()
    {
        Assert.Throws<DomainException>(() =>
            new ClaimEvaluationResult(ClaimStatus.Covered, 50, null, false, [], [], [], "r", null, [], "model"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_rejects_confidence_outside_range(int confidence)
    {
        Assert.Throws<DomainException>(() => Result(confidence: confidence));
    }

    [Fact]
    public void Create_requires_options_when_a_coverage_level_is_needed()
    {
        Assert.Throws<DomainException>(() => Result(ClaimStatus.InsufficientInformation, [], needsLevel: true));

        var result = Result(ClaimStatus.InsufficientInformation, [], needsLevel: true, options: ["Gold"]);
        Assert.True(result.NeedsCoverageLevel);
    }

    [Fact]
    public void Fail_stores_the_error_code_without_a_result()
    {
        var claim = Start();

        claim.Fail(ErrorCodes.ClaimEvaluationFailed, Now);

        Assert.Equal(ClaimState.Failed, claim.State);
        Assert.Equal(ErrorCodes.ClaimEvaluationFailed, claim.ErrorCode);
        Assert.Null(claim.Result);
    }

    [Fact]
    public void Complete_and_Fail_are_only_allowed_while_evaluating()
    {
        var claim = Start();
        claim.Complete(Result(), Now);

        Assert.Equal(DomainErrorKind.Conflict, Assert.Throws<DomainException>(() => claim.Fail(ErrorCodes.ClaimEvaluationFailed, Now)).Kind);
        Assert.Throws<DomainException>(() => claim.Complete(Result(), Now));
    }
}
