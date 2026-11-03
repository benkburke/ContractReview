namespace ContractReview.Application.Features.Claims.Progress;

/// <summary>Pushes a progress event to one connection. Never throws; does nothing without a connection id.</summary>
public interface IClaimProgressNotifier
{
    Task NotifyAsync(string? connectionId, ClaimProgressEvent progress);
}
