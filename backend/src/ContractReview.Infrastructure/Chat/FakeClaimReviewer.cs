using ContractReview.Application.Abstractions.Chat;
using ContractReview.Application.Features.Claims.Evaluation;
using ContractReview.Domain.Claims;

namespace ContractReview.Infrastructure.Chat;

/// <summary>
/// Deterministic, local stand-in for the reviewer model in tests, CI and e2e (D-028). It never calls the network.
/// <list type="bullet">
/// <item>The review agrees with the primary answer, copies its confidence and items, and says Covered is true only for
/// a Covered primary status.</item>
/// <item>If the claim text contains <see cref="DisagreeTrigger"/> (case-insensitive) the review disagrees with a
/// confidence of <see cref="DisagreeConfidence"/> and the opposite covered verdict, so tests can show disagreement.</item>
/// <item>If the claim text contains <see cref="FailTrigger"/> (case-insensitive) the review fails like an unreachable
/// model, so tests can show an unavailable review.</item>
/// </list>
/// </summary>
public sealed class FakeClaimReviewer : IClaimReviewer
{
    public const string Model = "fake/rule-based-claim-reviewer";
    public const string DisagreeTrigger = "disputed";
    public const string FailTrigger = "unreviewable";
    public const int DisagreeConfidence = 60;
    public const string AgreeExplanation = "The contract excerpts support the primary answer.";
    public const string DisagreeExplanation = "The contract excerpts do not clearly support the primary answer.";

    public string ModelName => Model;

    public Task<LlmClaimReview> ReviewAsync(
        ClaimPrompt prompt, ClaimEvaluationResult primary, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (prompt.ClaimText.Contains(FailTrigger, StringComparison.OrdinalIgnoreCase))
        {
            throw new ChatFailedException(ChatFailureReasons.TransportError);
        }

        var agrees = !prompt.ClaimText.Contains(DisagreeTrigger, StringComparison.OrdinalIgnoreCase);
        var primaryCovered = primary.Status == ClaimStatus.Covered;
        return Task.FromResult(new LlmClaimReview(
            agrees,
            agrees ? primaryCovered : !primaryCovered,
            agrees ? primary.Confidence : DisagreeConfidence,
            agrees ? AgreeExplanation : DisagreeExplanation,
            primary.CoveredItems,
            primary.ExcludedItems));
    }
}
