using ContractReview.Application.Abstractions.Messaging;
using ContractReview.Application.Abstractions.Persistence;
using ContractReview.Domain.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ContractReview.Application.Features.Contracts.Queries.GetContract;

public sealed class GetContractHandler(IAppDbContext db) : IQueryHandler<GetContractQuery, ContractDetailDto?>
{
    public async Task<ContractDetailDto?> HandleAsync(GetContractQuery query, CancellationToken cancellationToken)
    {
        var contract = await db.Contracts
            .AsNoTracking()
            .Where(c => c.Id == query.Id)
            .Select(c => new ContractDetailDto(
                c.Id, c.Title, c.FileName, c.SizeBytes, c.PageCount, c.UploadedAtUtc, c.ExtractedText,
                c.IndexStatus, Array.Empty<ContractSectionDto>()))
            .FirstOrDefaultAsync(cancellationToken);
        if (contract is not { IndexStatus: IndexStatus.Indexed })
        {
            return contract;
        }

        // Sections are grouped from chunk rows; embeddings are never read here.
        var sections = await db.Contracts
            .AsNoTracking()
            .Where(c => c.Id == query.Id)
            .SelectMany(c => c.Chunks)
            .GroupBy(ch => new { ch.SectionOrder, ch.SectionHeading })
            .OrderBy(g => g.Key.SectionOrder)
            .Select(g => new ContractSectionDto(g.Key.SectionOrder, g.Key.SectionHeading, g.Count()))
            .ToListAsync(cancellationToken);

        return contract with { Sections = sections };
    }
}
