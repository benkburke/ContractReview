namespace ContractReview.Application.Features.Claims.Progress;

/// <summary>One progress push to the caller's connection. ClaimId is set on Completed and Failed; ErrorCode only on Failed.</summary>
public sealed record ClaimProgressEvent(
    Guid ContractId,
    ClaimProgressStep Step,
    DateTime OccurredAtUtc,
    Guid? ClaimId = null,
    string? ErrorCode = null);
