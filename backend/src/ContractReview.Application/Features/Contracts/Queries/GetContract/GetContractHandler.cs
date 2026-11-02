using ContractReview.Application.Abstractions.Messaging;
using ContractReview.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ContractReview.Application.Features.Contracts.Queries.GetContract;

public sealed class GetContractHandler(IAppDbContext db) : IQueryHandler<GetContractQuery, ContractDetailDto?>
{
    public async Task<ContractDetailDto?> HandleAsync(GetContractQuery query, CancellationToken cancellationToken) =>
        await db.Contracts
            .AsNoTracking()
            .Where(c => c.Id == query.Id)
            .Select(c => new ContractDetailDto(
                c.Id, c.Title, c.FileName, c.SizeBytes, c.PageCount, c.UploadedAtUtc, c.ExtractedText))
            .FirstOrDefaultAsync(cancellationToken);
}
