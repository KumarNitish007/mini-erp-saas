using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common;
using MiniErp.Domain.Common;

namespace MiniErp.Api.Infrastructure;

public static class Policies
{
    public const string Manager = "Manager";
    public const string AuthRateLimit = "auth";
}

/// <summary>Maps application exceptions to RFC 7807 problem details.</summary>
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed", "One or more fields are invalid."),
            NotFoundException e => (StatusCodes.Status404NotFound, "Not found", e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrent update",
                "The record was changed by someone else. Reload and try again."),
            DomainException e => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", e.Message),
            AuthenticationFailedException e => (StatusCodes.Status401Unauthorized, "Unauthorized", e.Message),
            ForbiddenException e => (StatusCodes.Status403Forbidden, "Forbidden", e.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", "Authentication is required."),
            _ => (StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred.")
        };

        if (status >= 500) logger.LogError(exception, "Unhandled exception for {Method} {Path}", http.Request.Method, http.Request.Path);

        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        if (exception is ValidationException ve)
        {
            problem.Extensions["errors"] = ve.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => ToCamel(g.Key), g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
        }

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem, Exception = exception });
    }

    private static string ToCamel(string name) =>
        string.IsNullOrEmpty(name) ? name : string.Join('.', name.Split('.').Select(p => char.ToLowerInvariant(p[0]) + p[1..]));
}
