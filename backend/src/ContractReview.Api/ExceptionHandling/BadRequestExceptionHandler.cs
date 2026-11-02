using ContractReview.Api.Http;
using ContractReview.Domain.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ContractReview.Api.ExceptionHandling;

/// <summary>Turns request-reading failures (such as a body over the server limit) into ProblemDetails with their status.</summary>
public sealed class BadRequestExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        httpContext.Response.StatusCode = badRequest.StatusCode;
        var problem = new ProblemDetails { Status = badRequest.StatusCode };
        if (badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            problem.Detail = ErrorMessages.ContractFileTooLarge;
            problem.Extensions[ProblemExtensions.Code] = ErrorCodes.ContractFileTooLarge;
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
