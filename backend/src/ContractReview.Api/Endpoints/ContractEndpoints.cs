using ContractReview.Api.Http;
using ContractReview.Api.Routes;
using ContractReview.Application.Abstractions.Messaging;
using ContractReview.Application.Features.Contracts;
using ContractReview.Application.Features.Contracts.Commands.IndexContract;
using ContractReview.Application.Features.Contracts.Commands.UploadContract;
using ContractReview.Application.Features.Contracts.Queries.GetContract;
using ContractReview.Application.Features.Contracts.Queries.ListContracts;
using ContractReview.Domain.Errors;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace ContractReview.Api.Endpoints;

public static class ContractEndpoints
{
    public const string UploadContractName = "UploadContract";
    public const string ListContractsName = "ListContracts";
    public const string GetContractName = "GetContract";
    public const string IndexContractName = "IndexContract";

    public static IEndpointRouteBuilder MapContractEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Contracts).WithTags("Contracts");

        group.MapPost("/", UploadAsync)
            .WithName(UploadContractName)
            .DisableAntiforgery()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

        group.MapGet("/", ListAsync).WithName(ListContractsName);

        group.MapGet(ApiRoutes.ContractById, GetAsync)
            .WithName(GetContractName)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost(ApiRoutes.ContractIndex, IndexAsync)
            .WithName(IndexContractName)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return app;
    }

    private static async Task<CreatedAtRoute<ContractSummaryDto>> UploadAsync(
        [FromForm] UploadContractForm form,
        ICommandHandler<UploadContractCommand, ContractSummaryDto> handler,
        CancellationToken cancellationToken)
    {
        await using var stream = form.File.OpenReadStream();
        var result = await handler.HandleAsync(
            new UploadContractCommand(form.Title, form.File.FileName, form.File.ContentType, form.File.Length, stream),
            cancellationToken);
        return TypedResults.CreatedAtRoute(result, GetContractName, new { id = result.Id });
    }

    private static async Task<Ok<IReadOnlyList<ContractSummaryDto>>> ListAsync(
        IQueryHandler<ListContractsQuery, IReadOnlyList<ContractSummaryDto>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new ListContractsQuery(), cancellationToken));

    private static async Task<Results<Ok<ContractDetailDto>, ProblemHttpResult>> GetAsync(
        Guid id,
        IQueryHandler<GetContractQuery, ContractDetailDto?> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetContractQuery(id), cancellationToken);
        if (result is not null)
        {
            return TypedResults.Ok(result);
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Detail = ErrorMessages.ContractNotFound,
        };
        problem.Extensions[ProblemExtensions.Code] = ErrorCodes.ContractNotFound;
        return TypedResults.Problem(problem);
    }

    private static async Task<Results<Ok<ContractDetailDto>, ProblemHttpResult>> IndexAsync(
        Guid id,
        ICommandHandler<IndexContractCommand, ContractDetailDto?> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new IndexContractCommand(id), cancellationToken);
        if (result is not null)
        {
            return TypedResults.Ok(result);
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Detail = ErrorMessages.ContractNotFound,
        };
        problem.Extensions[ProblemExtensions.Code] = ErrorCodes.ContractNotFound;
        return TypedResults.Problem(problem);
    }

    public sealed record UploadContractForm(string Title, IFormFile File);
}
