using ContractReview.Application.Abstractions.Messaging;
using ContractReview.Application.Abstractions.Pdf;
using ContractReview.Application.Abstractions.Persistence;
using ContractReview.Application.Options;
using ContractReview.Domain.Contracts;
using ContractReview.Domain.Errors;
using Microsoft.Extensions.Options;

namespace ContractReview.Application.Features.Contracts.Commands.UploadContract;

public sealed class UploadContractHandler(
    IAppDbContext db,
    IPdfTextExtractor extractor,
    IOptions<UploadOptions> options,
    TimeProvider clock) : ICommandHandler<UploadContractCommand, ContractSummaryDto>
{
    private const int ReadChunkBytes = 81920;

    public async Task<ContractSummaryDto> HandleAsync(UploadContractCommand command, CancellationToken cancellationToken)
    {
        var maxFileBytes = options.Value.MaxFileBytes;

        // Cheap checks first so nothing is read or parsed for an invalid request.
        Contract.NormalizeTitle(command.Title);
        Contract.EnsureFile(command.FileName, command.SizeBytes, maxFileBytes);
        if (!IsPdfContentType(command.ContentType))
        {
            throw NotPdf();
        }

        var content = await ReadContentAsync(command.Content, maxFileBytes, cancellationToken);
        if (!content.AsSpan().StartsWith(ContractRules.PdfHeader))
        {
            throw NotPdf();
        }

        PdfExtractionResult extraction;
        try
        {
            extraction = extractor.Extract(content);
        }
        catch (PdfUnreadableException)
        {
            throw new DomainException(ErrorCodes.ContractPdfUnreadable, ErrorMessages.ContractPdfUnreadable);
        }

        var contract = Contract.Upload(
            command.Title,
            command.FileName,
            ContractRules.PdfContentType,
            content,
            extraction.Text,
            extraction.PageCount,
            maxFileBytes,
            clock.GetUtcNow().UtcDateTime);

        db.Contracts.Add(contract);
        await db.SaveChangesAsync(cancellationToken);

        return new ContractSummaryDto(
            contract.Id, contract.Title, contract.FileName, contract.SizeBytes, contract.PageCount, contract.UploadedAtUtc);
    }

    private static DomainException NotPdf() =>
        new(ErrorCodes.ContractFileNotPdf, ErrorMessages.ContractFileNotPdf);

    private static bool IsPdfContentType(string? contentType)
    {
        var mediaType = contentType?.Split(ContractRules.ContentTypeParameterSeparator)[0].Trim();
        return string.Equals(mediaType, ContractRules.PdfContentType, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Stops reading once the limit is exceeded, so an understated length cannot bypass it.</summary>
    private static async Task<byte[]> ReadContentAsync(Stream stream, long maxFileBytes, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[ReadChunkBytes];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            if (buffer.Length > maxFileBytes)
            {
                throw new DomainException(ErrorCodes.ContractFileTooLarge, ErrorMessages.ContractFileTooLarge);
            }
        }

        return buffer.ToArray();
    }
}
