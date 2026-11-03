using ContractReview.Application.Abstractions.Chat;
using ContractReview.Domain.Claims;

namespace ContractReview.Application.Features.Claims.Evaluation;

/// <summary>Asks a second model to review the primary answer against the same retrieved chunks.</summary>
public interface IClaimReviewer
{
    /// <summary>The reviewer model name stored with the review.</summary>
    string ModelName { get; }

    /// <summary>
    /// Returns the reviewer's validated verdict. Throws <see cref="ChatFailedException"/> on any failure: not configured,
    /// HTTP error, timeout, or output that is still invalid after the one retry.
    /// </summary>
    Task<LlmClaimReview> ReviewAsync(ClaimPrompt prompt, ClaimEvaluationResult primary, CancellationToken cancellationToken);
}
