using ContractReview.Application.Abstractions.Messaging;
using ContractReview.Application.Abstractions.Persistence;
using ContractReview.Domain.Claims;
using ContractReview.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace ContractReview.Application.Features.Claims.Queries.ListClaims;

/// <summary>
/// Returns one page of a contract's claims, newest first. Throws a Validation <see cref="DomainException"/> with
/// <c>paging.invalid</c> for out-of-range paging and a NotFound one with <c>contract.not_found</c> for an unknown contract.
/// </summary>
public sealed class ListClaimsHandler(IAppDbContext db) : IQueryHandler<ListClaimsQuery, ClaimHistoryPageDto>
{
    public const int MaxPage = 10_000;
    public const int MaxPageSize = 100;

    public async Task<ClaimHistoryPageDto> HandleAsync(ListClaimsQuery request, CancellationToken cancellationToken)
    {
        if (request.Page is < 1 or > MaxPage || request.PageSize is < 1 or > MaxPageSize)
        {
            throw new DomainException(ErrorCodes.PagingInvalid, ErrorMessages.PagingInvalid, DomainErrorKind.Validation);
        }

        if (!await db.Contracts.AnyAsync(c => c.Id == request.ContractId, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ContractNotFound, ErrorMessages.ContractNotFound, DomainErrorKind.NotFound);
        }

        var query = db.Claims.AsNoTracking().Where(c => c.ContractId == request.ContractId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .ThenByDescending(c => c.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new ClaimSummaryDto(
                c.Id, c.ClaimText, c.State ?? ClaimState.Failed, c.Status, c.Confidence, c.ReviewAgrees, c.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return new ClaimHistoryPageDto(items, request.Page, request.PageSize, total);
    }
}
