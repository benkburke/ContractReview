using ContractReview.Application.Abstractions.Chat;
using ContractReview.Application.Abstractions.Embeddings;
using ContractReview.Application.Abstractions.Messaging;
using ContractReview.Application.Abstractions.Persistence;
using ContractReview.Application.Features.Claims.Progress;
using ContractReview.Application.Features.Claims.Retrieval;
using ContractReview.Application.Options;
using ContractReview.Domain.Claims;
using ContractReview.Domain.Contracts;
using ContractReview.Domain.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractReview.Application.Features.Claims.Commands.EvaluateClaim;

/// <summary>
/// Validates the claim, checks the contract is Indexed with the current embedding model, embeds the claim, retrieves the
/// top chunks, asks the chat model and stores the claim in one save. Citations to chunks that were not retrieved are
/// dropped; a decisive answer left without citations becomes InsufficientInformation (D-032). Any embedding, retrieval
/// or chat failure stores the claim as Failed (D-033) and throws <see cref="ClaimEvaluationFailedException"/>.
/// Returns null when the contract does not exist.
/// </summary>
public sealed class EvaluateClaimHandler(
    IAppDbContext db,
    IEmbeddingClient embeddings,
    IClaimRetriever retriever,
    IChatCompletionClient chat,
    IOptions<ClaimsOptions> options,
    IClaimProgressNotifier notifier,
    TimeProvider clock,
    ILogger<EvaluateClaimHandler> logger) : ICommandHandler<EvaluateClaimCommand, ClaimEvaluationDto?>
{
    public const string ClaimTextSeparator = "\n";

    public async Task<ClaimEvaluationDto?> HandleAsync(EvaluateClaimCommand command, CancellationToken cancellationToken)
    {
        var claim = Claim.Start(command.ContractId, command.ClaimText, command.CoverageLevel, UtcNow());

        var contract = await db.Contracts
            .AsNoTracking()
            .Where(c => c.Id == command.ContractId)
            .Select(c => new { c.IndexStatus, c.EmbeddingModel })
            .FirstOrDefaultAsync(cancellationToken);
        if (contract is null)
        {
            return null;
        }

        if (contract.IndexStatus != IndexStatus.Indexed)
        {
            throw new DomainException(
                ErrorCodes.ContractNotIndexed, ErrorMessages.ContractNotIndexed, DomainErrorKind.Conflict);
        }

        if (contract.EmbeddingModel != embeddings.ModelName)
        {
            throw new DomainException(
                ErrorCodes.ContractEmbeddingModelMismatch,
                ErrorMessages.ContractEmbeddingModelMismatch,
                DomainErrorKind.Conflict);
        }

        try
        {
            claim.Complete(await EvaluateAsync(claim, command.ProgressConnectionId, cancellationToken), UtcNow());
        }
        catch (Exception ex) when (ex is EmbeddingFailedException or ChatFailedException or ClaimEvaluationFailedException or DomainException)
        {
            logger.LogWarning(ex, "Evaluating claim {ClaimId} for contract {ContractId} failed", claim.Id, claim.ContractId);
            claim.Fail(ErrorCodes.ClaimEvaluationFailed, UtcNow());
            db.Claims.Add(claim);
            await db.SaveChangesAsync(CancellationToken.None);
            await PushAsync(command.ProgressConnectionId, claim, ClaimProgressStep.Failed, ErrorCodes.ClaimEvaluationFailed);
            throw new ClaimEvaluationFailedException(ex);
        }

        db.Claims.Add(claim);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Evaluated claim {ClaimId} for contract {ContractId}: {Status}", claim.Id, claim.ContractId, claim.Status);
        await PushAsync(command.ProgressConnectionId, claim, ClaimProgressStep.Completed);
        return ClaimMapping.ToDto(claim);
    }

    private async Task<ClaimEvaluationResult> EvaluateAsync(
        Claim claim, string? connectionId, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        await PushAsync(connectionId, claim, ClaimProgressStep.Retrieving);
        var queryText = claim.CoverageLevelRequested is null
            ? claim.ClaimText
            : claim.ClaimText + ClaimTextSeparator + claim.CoverageLevelRequested;
        var vectors = await embeddings.EmbedAsync([queryText], cancellationToken);
        if (vectors.Count != 1 || vectors[0] is not { Length: > 0 } query)
        {
            throw new EmbeddingFailedException(EmbeddingFailureReasons.InvalidResponse);
        }

        var candidates = await db.Contracts
            .AsNoTracking()
            .Where(c => c.Id == claim.ContractId)
            .SelectMany(c => c.Chunks)
            .Select(ch => new ChunkCandidate(ch.SectionOrder, ch.SectionHeading, ch.ChunkOrder, ch.Text, ch.Embedding))
            .ToListAsync(cancellationToken);
        var retrieved = retriever.Retrieve(query, candidates, settings.TopK, settings.MaxContextChars);
        if (retrieved.Count == 0)
        {
            throw new ClaimEvaluationFailedException();
        }

        var prompt = new ClaimPrompt(
            claim.ClaimText,
            claim.CoverageLevelRequested,
            retrieved.Select(r => new PromptChunk(r.Label, r.SectionHeading, r.Text)).ToList());
        await PushAsync(connectionId, claim, ClaimProgressStep.Reasoning);
        var answer = await chat.EvaluateAsync(prompt, cancellationToken);
        if (answer.FindViolation() is not null)
        {
            throw new ChatFailedException(ChatFailureReasons.MalformedOutput);
        }

        return ClaimEvaluationResult.Create(
            answer.Status,
            answer.Confidence,
            answer.CoverageLevel ?? claim.CoverageLevelRequested,
            answer.NeedsCoverageLevel,
            answer.CoverageLevelOptions,
            answer.CoveredItems,
            answer.ExcludedItems,
            answer.Reasoning,
            answer.FollowUpQuestion,
            ValidCitations(answer.CitedLabels, retrieved),
            chat.ModelName);
    }

    /// <summary>Keeps only labels that were retrieved, once each, as citations with an excerpt of the stored chunk text.</summary>
    private static List<ClaimCitation> ValidCitations(IReadOnlyList<string> labels, IReadOnlyList<RetrievedChunk> retrieved)
    {
        var citations = new List<ClaimCitation>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in labels.Select(l => l.Trim().Trim('[', ']')))
        {
            var chunk = retrieved.FirstOrDefault(r => string.Equals(r.Label, label, StringComparison.OrdinalIgnoreCase));
            if (chunk is null || !seen.Add(chunk.Label))
            {
                continue;
            }

            var text = chunk.Text.Trim();
            var excerpt = text.Length > ClaimRules.MaxExcerptLength ? text[..ClaimRules.MaxExcerptLength] : text;
            citations.Add(new ClaimCitation(chunk.SectionHeading, excerpt));
        }

        return citations;
    }

    private Task PushAsync(string? connectionId, Claim claim, ClaimProgressStep step, string? errorCode = null) =>
        notifier.NotifyAsync(
            connectionId,
            new ClaimProgressEvent(
                claim.ContractId,
                step,
                UtcNow(),
                step is ClaimProgressStep.Completed or ClaimProgressStep.Failed ? claim.Id : null,
                errorCode));

    private DateTime UtcNow() => clock.GetUtcNow().UtcDateTime;
}
