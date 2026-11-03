using ContractReview.Application.Abstractions.Messaging;
using ContractReview.Application.Abstractions.Persistence;
using ContractReview.Domain.Claims;
using ContractReview.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace ContractReview.Application.Features.Claims.Queries.GetClaim;

/// <summary>
/// Returns the stored claim. Throws a NotFound <see cref="DomainException"/> with <c>claim.not_found</c> when the
/// claim does not exist for the contract, or <c>contract.not_found</c> when the contract itself does not exist.
/// </summary>
public sealed class GetClaimHandler(IAppDbContext db) : IQueryHandler<GetClaimQuery, ClaimEvaluationDto>
{
    public async Task<ClaimEvaluationDto> HandleAsync(GetClaimQuery request, CancellationToken cancellationToken)
    {
        var row = await db.Claims
            .AsNoTracking()
            .Where(c => c.Id == request.ClaimId && c.ContractId == request.ContractId)
            .Select(c => new
            {
                c.Id,
                c.ContractId,
                c.ClaimText,
                c.CoverageLevelRequested,
                c.State,
                c.CreatedAtUtc,
                c.ErrorCode,
                c.Status,
                c.Confidence,
                c.CoverageLevel,
                c.NeedsCoverageLevel,
                c.ResultJson,
                c.Model,
                c.ReviewState,
                c.ReviewAgrees,
                c.ReviewIsCovered,
                c.ReviewConfidence,
                c.ReviewJson,
                c.ReviewModel,
                c.ReviewErrorCode,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            var contractExists = await db.Contracts.AnyAsync(c => c.Id == request.ContractId, cancellationToken);
            throw contractExists
                ? new DomainException(ErrorCodes.ClaimNotFound, ErrorMessages.ClaimNotFound, DomainErrorKind.NotFound)
                : new DomainException(ErrorCodes.ContractNotFound, ErrorMessages.ContractNotFound, DomainErrorKind.NotFound);
        }

        var result = row.Status is { } status && row.Confidence is { } confidence && row.ResultJson is not null && row.Model is not null
            ? ClaimEvaluationResult.FromStored(
                status, confidence, row.CoverageLevel, row.NeedsCoverageLevel, row.Model, row.ResultJson)
            : null;
        var review = ClaimReview.FromStored(
            row.ReviewState, row.ReviewAgrees, row.ReviewIsCovered, row.ReviewConfidence, row.ReviewModel, row.ReviewErrorCode, row.ReviewJson);
        return new ClaimEvaluationDto(
            row.Id,
            row.ContractId,
            row.ClaimText,
            row.CoverageLevelRequested,
            row.State ?? ClaimState.Failed,
            row.CreatedAtUtc,
            result is null ? null : ClaimMapping.ToDto(result),
            row.ErrorCode,
            review is null ? null : ClaimMapping.ToDto(review));
    }
}
