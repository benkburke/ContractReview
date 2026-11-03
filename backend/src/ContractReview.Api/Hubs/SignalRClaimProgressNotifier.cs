using ContractReview.Application.Features.Claims.Progress;
using Microsoft.AspNetCore.SignalR;

namespace ContractReview.Api.Hubs;

public sealed class SignalRClaimProgressNotifier(
    IHubContext<ClaimProgressHub, IClaimProgressClient> hub,
    ILogger<SignalRClaimProgressNotifier> logger) : IClaimProgressNotifier
{
    public async Task NotifyAsync(string? connectionId, ClaimProgressEvent progress)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            return;
        }

        try
        {
            await hub.Clients.Client(connectionId).EvaluationProgress(progress);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Pushing {Step} progress for contract {ContractId} failed", progress.Step, progress.ContractId);
        }
    }
}
