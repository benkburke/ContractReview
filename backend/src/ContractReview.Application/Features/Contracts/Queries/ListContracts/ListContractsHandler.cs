using ContractReview.Application.Abstractions.Messaging;
using ContractReview.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ContractReview.Application.Features.Contracts.Queries.ListContracts;

public sealed class ListContractsHandler(IAppDbContext db)
    : IQueryHandler<ListContractsQuery, IReadOnlyList<ContractSummaryDto>>
{
    public async Task<IReadOnlyList<ContractSummaryDto>> HandleAsync(
        ListContractsQuery query, CancellationToken cancellationToken) =>
        await db.Contracts
            .AsNoTracking()
            .OrderByDescending(c => c.UploadedAtUtc)
            .ThenByDescending(c => c.Id)
            .Select(c => new ContractSummaryDto(c.Id, c.Title, c.FileName, c.SizeBytes, c.PageCount, c.UploadedAtUtc))
            .ToListAsync(cancellationToken);
}
