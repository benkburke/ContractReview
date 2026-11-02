using ContractReview.Api.Http;
using ContractReview.Application.Abstractions.Embeddings;
using ContractReview.Domain.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ContractReview.Api.ExceptionHandling;

/// <summary>Maps an embedding service failure to 502 ProblemDetails with <c>contract.embedding_failed</c>; the reason stays in the logs.</summary>
public sealed class EmbeddingFailedExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not EmbeddingFailedException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status502BadGateway,
            Detail = ErrorMessages.ContractEmbeddingFailed,
        };
        problem.Extensions[ProblemExtensions.Code] = ErrorCodes.ContractEmbeddingFailed;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
