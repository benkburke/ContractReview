using ContractReview.Api.Http;
using ContractReview.Domain.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ContractReview.Api.ExceptionHandling;

public sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domain)
        {
            return false;
        }

        var status = domain.Kind == DomainErrorKind.Conflict
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;
        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails { Status = status, Detail = domain.Message };
        problem.Extensions[ProblemExtensions.Code] = domain.Code;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
