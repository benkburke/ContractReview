using ContractReview.Api.Http;
using ContractReview.Application.Features.Claims;
using ContractReview.Domain.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ContractReview.Api.ExceptionHandling;

/// <summary>Maps a failed claim evaluation to 502 ProblemDetails with <c>claim.evaluation_failed</c>; the cause stays in the logs.</summary>
public sealed class ClaimEvaluationFailedExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ClaimEvaluationFailedException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status502BadGateway,
            Detail = ErrorMessages.ClaimEvaluationFailed,
        };
        problem.Extensions[ProblemExtensions.Code] = ErrorCodes.ClaimEvaluationFailed;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
