namespace ContractReview.Application.Features.Claims.Progress;

public interface IClaimProgressClient
{
    Task EvaluationProgress(ClaimProgressEvent e);
}
