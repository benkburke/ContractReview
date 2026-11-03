using ContractReview.Application.Features.Claims.Progress;
using Microsoft.AspNetCore.SignalR;

namespace ContractReview.Api.Hubs;

public sealed class ClaimProgressHub : Hub<IClaimProgressClient>
{
    public const string EvaluationProgressMethod = nameof(IClaimProgressClient.EvaluationProgress);
}
