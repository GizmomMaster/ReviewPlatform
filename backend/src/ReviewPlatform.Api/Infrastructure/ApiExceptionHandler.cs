using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Api.Infrastructure;

/// <summary>Исключения приложения → ProblemDetails: валидация 400, не найдено 404, бизнес-правило 409.</summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
            ValidationException validation => new HttpValidationProblemDetails(
                validation.Errors
                    .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()))
            {
                Title = "Ошибка валидации",
                Status = StatusCodes.Status400BadRequest,
            },
            NotFoundException => new ProblemDetails { Title = "Не найдено", Status = StatusCodes.Status404NotFound },
            DomainException => new ProblemDetails { Title = "Нарушено бизнес-правило", Detail = exception.Message, Status = StatusCodes.Status409Conflict },
            _ => null,
        };
        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, Exception = exception, ProblemDetails = problem });
    }
}
