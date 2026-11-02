namespace ContractReview.Application.Features.Claims;

/// <summary>The claim could not be evaluated (embedding, retrieval or chat failure); it was stored as Failed. Maps to 502.</summary>
public sealed class ClaimEvaluationFailedException(Exception? innerException = null)
    : Exception(nameof(ClaimEvaluationFailedException), innerException);
