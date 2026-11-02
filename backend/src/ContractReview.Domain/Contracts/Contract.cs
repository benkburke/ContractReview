using ContractReview.Domain.Errors;

namespace ContractReview.Domain.Contracts;

public sealed class Contract
{
    private readonly List<ContractChunk> chunks = [];

    private Contract()
    {
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public int PageCount { get; private set; }

    public byte[] FileContent { get; private set; } = [];

    public string ExtractedText { get; private set; } = string.Empty;

    public DateTime UploadedAtUtc { get; private set; }

    public IndexStatus IndexStatus { get; private set; } = IndexStatus.NotIndexed;

    /// <summary>The error code of the last failed indexing attempt; null unless <see cref="IndexStatus"/> is Failed.</summary>
    public string? IndexError { get; private set; }

    public DateTime? IndexedAtUtc { get; private set; }

    public string? EmbeddingModel { get; private set; }

    public int? EmbeddingDimensions { get; private set; }

    public IReadOnlyCollection<ContractChunk> Chunks => chunks;

    public static Contract Upload(
        string? title,
        string? fileName,
        string contentType,
        byte[] content,
        string? extractedText,
        int pageCount,
        long maxFileBytes,
        DateTime uploadedAtUtc)
    {
        var normalizedTitle = NormalizeTitle(title);
        EnsureFile(fileName, content.LongLength, maxFileBytes);

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            throw new DomainException(ErrorCodes.ContractNoText, ErrorMessages.ContractNoText);
        }

        if (pageCount < 1)
        {
            throw new DomainException(ErrorCodes.ContractPdfUnreadable, ErrorMessages.ContractPdfUnreadable);
        }

        return new Contract
        {
            Id = Guid.NewGuid(),
            Title = normalizedTitle,
            FileName = fileName!,
            ContentType = contentType,
            SizeBytes = content.LongLength,
            PageCount = pageCount,
            FileContent = content,
            ExtractedText = extractedText,
            UploadedAtUtc = uploadedAtUtc,
        };
    }

    /// <summary>Trims the title and checks it is 1 to <see cref="ContractRules.MaxTitleLength"/> characters.</summary>
    public static string NormalizeTitle(string? title)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > ContractRules.MaxTitleLength)
        {
            throw new DomainException(ErrorCodes.ContractTitleInvalid, ErrorMessages.ContractTitleInvalid);
        }

        return trimmed;
    }

    /// <summary>Checks the file name ends in .pdf (1 to 255 characters) and the size is within 1 byte to the limit.</summary>
    public static void EnsureFile(string? fileName, long sizeBytes, long maxFileBytes)
    {
        if (string.IsNullOrEmpty(fileName)
            || fileName.Length > ContractRules.MaxFileNameLength
            || !fileName.EndsWith(ContractRules.PdfExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(ErrorCodes.ContractFileNotPdf, ErrorMessages.ContractFileNotPdf);
        }

        if (sizeBytes < 1)
        {
            throw new DomainException(ErrorCodes.ContractFileNotPdf, ErrorMessages.ContractFileNotPdf);
        }

        if (sizeBytes > maxFileBytes)
        {
            throw new DomainException(ErrorCodes.ContractFileTooLarge, ErrorMessages.ContractFileTooLarge);
        }
    }

    /// <summary>
    /// Moves the contract to Indexing. Throws <see cref="ErrorCodes.ContractIndexingInProgress"/> when it is already
    /// Indexing, unless <paramref name="resumeStale"/> is set (the startup backfill taking over an interrupted run).
    /// </summary>
    public void StartIndexing(bool resumeStale = false)
    {
        if (IndexStatus == IndexStatus.Indexing && !resumeStale)
        {
            throw new DomainException(
                ErrorCodes.ContractIndexingInProgress, ErrorMessages.ContractIndexingInProgress, DomainErrorKind.Conflict);
        }

        IndexStatus = IndexStatus.Indexing;
        IndexError = null;
    }

    /// <summary>Atomically replaces all chunks and marks the contract Indexed. Only allowed while Indexing.</summary>
    public void ReplaceChunks(IReadOnlyList<ContractChunkData> newChunks, string model, int dimensions, DateTime indexedAtUtc)
    {
        EnsureIndexing();
        if (newChunks.Count == 0)
        {
            throw new DomainException(ErrorCodes.ContractNoText, ErrorMessages.ContractNoText, DomainErrorKind.Conflict);
        }

        if (string.IsNullOrWhiteSpace(model) || model.Length > ContractRules.MaxEmbeddingModelLength || dimensions < 1)
        {
            throw RuleViolated();
        }

        var positions = new HashSet<(int SectionOrder, int ChunkOrder)>();
        foreach (var chunk in newChunks)
        {
            if (chunk.Vector.Length != dimensions
                || chunk.SectionOrder < 0
                || chunk.ChunkOrder < 0
                || string.IsNullOrWhiteSpace(chunk.SectionHeading)
                || chunk.SectionHeading.Length > ContractRules.MaxSectionHeadingLength
                || string.IsNullOrWhiteSpace(chunk.Text)
                || !positions.Add((chunk.SectionOrder, chunk.ChunkOrder)))
            {
                throw RuleViolated();
            }
        }

        chunks.Clear();
        chunks.AddRange(newChunks.Select(chunk => ContractChunk.Create(Id, chunk)));
        EmbeddingModel = model;
        EmbeddingDimensions = dimensions;
        IndexedAtUtc = indexedAtUtc;
        IndexError = null;
        IndexStatus = IndexStatus.Indexed;
    }

    /// <summary>Marks the running indexing attempt as Failed with an error code. Existing chunks are kept unchanged.</summary>
    public void FailIndexing(string errorCode)
    {
        EnsureIndexing();
        if (string.IsNullOrWhiteSpace(errorCode) || errorCode.Length > ContractRules.MaxIndexErrorLength)
        {
            throw RuleViolated();
        }

        IndexError = errorCode;
        IndexStatus = IndexStatus.Failed;
    }

    private void EnsureIndexing()
    {
        if (IndexStatus != IndexStatus.Indexing)
        {
            throw new DomainException(ErrorCodes.Conflict, ErrorMessages.Conflict, DomainErrorKind.Conflict);
        }
    }

    private static DomainException RuleViolated() =>
        new(ErrorCodes.DomainRuleViolated, ErrorMessages.DomainRuleViolated);
}
