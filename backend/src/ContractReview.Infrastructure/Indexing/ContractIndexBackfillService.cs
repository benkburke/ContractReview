using ContractReview.Application.Abstractions.Messaging;
using ContractReview.Application.Abstractions.Persistence;
using ContractReview.Application.Features.Contracts;
using ContractReview.Application.Features.Contracts.Commands.IndexContract;
using ContractReview.Application.Options;
using ContractReview.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractReview.Infrastructure.Indexing;

/// <summary>
/// Runs once at startup (unless <see cref="IndexingOptions.BackfillOnStartup"/> is false): indexes every contract
/// uploaded before startup that is NotIndexed, Failed or left Indexing by an interrupted run, one at a time.
/// Failures are logged and never stop the API.
/// </summary>
public sealed class ContractIndexBackfillService(
    IServiceScopeFactory scopes,
    IOptions<IndexingOptions> options,
    TimeProvider clock,
    ILogger<ContractIndexBackfillService> logger) : BackgroundService
{
    /// <summary>Indexes all pending contracts once; callable directly (for example from tests).</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var startedAtUtc = clock.GetUtcNow().UtcDateTime;
        List<Guid> ids;
        try
        {
            ids = await FindPendingAsync(startedAtUtc, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Index backfill could not list contracts to index");
            return;
        }

        logger.LogInformation("Index backfill found {ContractCount} contracts to index", ids.Count);
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider
                    .GetRequiredService<ICommandHandler<IndexContractCommand, ContractDetailDto?>>();
                await handler.HandleAsync(new IndexContractCommand(id, ResumeStale: true), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Index backfill failed for contract {ContractId}", id);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.BackfillOnStartup)
        {
            return;
        }

        // Never hold up host startup with the database query.
        await Task.Yield();
        try
        {
            await RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down; the rest is picked up on the next start.
        }
    }

    private async Task<List<Guid>> FindPendingAsync(DateTime startedAtUtc, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return await db.Contracts
            .AsNoTracking()
            .Where(c => c.IndexStatus != IndexStatus.Indexed && c.UploadedAtUtc <= startedAtUtc)
            .OrderBy(c => c.UploadedAtUtc)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
    }
}
