using Microsoft.AspNetCore.Diagnostics;
using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Api.Infrastructure;

/// <summary>Нарушение бизнес-правила → 409 Conflict в формате ProblemDetails.</summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Title = "Нарушено бизнес-правило", Detail = exception.Message, Status = StatusCodes.Status409Conflict },
        });
    }
}
