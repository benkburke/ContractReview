using ContractReview.Application.Abstractions.Embeddings;
using ContractReview.Application.Abstractions.Messaging;
using ContractReview.Application.Abstractions.Persistence;
using ContractReview.Application.Features.Contracts.Indexing;
using ContractReview.Application.Options;
using ContractReview.Domain.Contracts;
using ContractReview.Domain.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractReview.Application.Features.Contracts.Commands.IndexContract;

/// <summary>
/// Claims the contract (Indexing, saved so concurrent callers get 409), chunks and embeds its text, then replaces all
/// chunks and marks it Indexed in one save. A chunking or embedding failure marks it Failed and is rethrown.
/// </summary>
public sealed class IndexContractHandler(
    IAppDbContext db,
    IContractChunker chunker,
    IEmbeddingClient embeddings,
    IOptions<IndexingOptions> options,
    TimeProvider clock,
    ILogger<IndexContractHandler> logger) : ICommandHandler<IndexContractCommand, ContractDetailDto?>
{
    public async Task<ContractDetailDto?> HandleAsync(IndexContractCommand command, CancellationToken cancellationToken)
    {
        var contract = await db.Contracts
            .Include(c => c.Chunks)
            .FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (contract is null)
        {
            return null;
        }

        contract.StartIndexing(command.ResumeStale);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another caller claimed the contract between this read and this save.
            throw InProgress();
        }

        List<ContractChunkData> chunks;
        try
        {
            chunks = await BuildChunksAsync(contract.ExtractedText, cancellationToken);
        }
        catch (Exception ex) when (ex is DomainException or EmbeddingFailedException or OperationCanceledException)
        {
            var code = ex is DomainException domain ? domain.Code : ErrorCodes.ContractEmbeddingFailed;
            logger.LogWarning(ex, "Indexing contract {ContractId} failed with {ErrorCode}", contract.Id, code);
            contract.FailIndexing(code);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        // One save: old chunks deleted, new chunks inserted and status Indexed in a single transaction.
        contract.ReplaceChunks(chunks, embeddings.ModelName, chunks[0].Vector.Length, clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Indexed contract {ContractId}: {ChunkCount} chunks with {EmbeddingModel}",
            contract.Id,
            chunks.Count,
            embeddings.ModelName);

        return new ContractDetailDto(
            contract.Id,
            contract.Title,
            contract.FileName,
            contract.SizeBytes,
            contract.PageCount,
            contract.UploadedAtUtc,
            contract.ExtractedText,
            contract.IndexStatus,
            ContractSections.FromChunks(contract.Chunks));
    }

    private static DomainException InProgress() =>
        new(ErrorCodes.ContractIndexingInProgress, ErrorMessages.ContractIndexingInProgress, DomainErrorKind.Conflict);

    private async Task<List<ContractChunkData>> BuildChunksAsync(string text, CancellationToken cancellationToken)
    {
        var drafts = chunker.Split(text);
        if (drafts.Count == 0)
        {
            throw new DomainException(ErrorCodes.ContractNoText, ErrorMessages.ContractNoText, DomainErrorKind.Conflict);
        }

        if (drafts.Count > options.Value.MaxChunks)
        {
            throw new DomainException(
                ErrorCodes.ContractTooManyChunks, ErrorMessages.ContractTooManyChunks, DomainErrorKind.Conflict);
        }

        var inputs = drafts
            .Select(d => d.SectionHeading + ChunkingRules.EmbeddingHeadingSeparator + d.Text)
            .ToList();
        var vectors = new List<float[]>(inputs.Count);
        foreach (var batch in inputs.Chunk(options.Value.EmbeddingBatchSize))
        {
            var result = await embeddings.EmbedAsync(batch, cancellationToken);
            if (result.Count != batch.Length)
            {
                throw new EmbeddingFailedException(EmbeddingFailureReasons.CountMismatch);
            }

            vectors.AddRange(result);
        }

        var dimensions = vectors[0]?.Length ?? 0;
        if (dimensions == 0 || vectors.Any(v => v is null || v.Length != dimensions))
        {
            throw new EmbeddingFailedException(EmbeddingFailureReasons.DimensionMismatch);
        }

        return drafts
            .Select((d, i) => new ContractChunkData(d.SectionOrder, d.SectionHeading, d.ChunkOrder, d.Text, vectors[i]))
            .ToList();
    }
}
