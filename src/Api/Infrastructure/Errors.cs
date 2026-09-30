using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
namespace AussieAlerts.Infrastructure;

public sealed class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && ct.IsCancellationRequested) return false;
        var status = exception switch
        {
            ValidationException => 400, ApiException e => e.Status, BadHttpRequestException => 400, _ => 500
        };
        ProblemDetails problem = exception is ValidationException v
            ? new ValidationProblemDetails(v.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = status == 500 ? "An unexpected error occurred." : exception is ValidationException ? "Validation failed." : exception.Message;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (status == 500) logger.LogError(exception, "Unhandled failure {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem, cancellationToken: ct);
        return true;
    }
}
