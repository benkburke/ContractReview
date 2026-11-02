using ContractReview.Application.Abstractions.Messaging;

namespace ContractReview.Application.Features.Contracts.Commands.UploadContract;

public sealed record UploadContractCommand(
    string Title,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content) : ICommand<ContractSummaryDto>;
