namespace ContractReview.Domain.Claims;

/// <summary>Outcome of the second-pass review; a claim that was not reviewed has no review at all.</summary>
public enum ReviewState
{
    Completed,
    Failed,
}
