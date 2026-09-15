using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Flow.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Flow.API.Middleware;

/// <summary>
/// Turns application and domain exceptions into RFC7807 ProblemDetails.
///
/// Every response carries the trace id, which is the same value stored on the audit entry
/// for the request, so a user-reported error can be tied to logs, traces and governance
/// history from a single string.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogError(exception, "Exception occurred after response had started; cannot write error response.");
            return;
        }

        var (statusCode, title, errors) = exception switch
        {
            ValidationException ve =>
                (HttpStatusCode.UnprocessableEntity, "Validation failed", (object?)ve.Errors),
            NotFoundException nfe =>
                (HttpStatusCode.NotFound, nfe.Message, (object?)null),
            ConflictException ce =>
                (HttpStatusCode.Conflict, ce.Message, (object?)null),
            // 401 is "I do not know who you are", 403 is "I know, and no". The client
            // reacts differently to each, so they must not be collapsed.
            UnauthorizedException ue =>
                (HttpStatusCode.Unauthorized, ue.Message, (object?)null),
            ForbiddenException fe =>
                (HttpStatusCode.Forbidden, fe.Message, (object?)null),
            // The assistant being unreachable is a degraded dependency, not a client error
            // and not a broken product: everything else keeps working.
            Flow.Application.Assistant.AssistantUnavailableException au =>
                (HttpStatusCode.ServiceUnavailable, au.Message, (object?)null),
            Flow.Domain.Exceptions.DomainException de =>
                (HttpStatusCode.Conflict, de.Message, (object?)null),
            OperationCanceledException =>
                ((HttpStatusCode)499, "The request was cancelled.", (object?)null),
            _ =>
                (HttpStatusCode.InternalServerError, "An unexpected error occurred.", (object?)null)
        };

        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Unhandled exception.");

        var problem = new ProblemDetails
        {
            Title = title,
            Status = (int)statusCode,
            Type = $"https://httpstatuses.io/{(int)statusCode}",
            Instance = context.Request.Path
        };

        if (errors is not null)
            problem.Extensions["errors"] = errors;

        // Domain and application exception messages are written for developers and are in
        // English, like the rest of the code — and they end up in Title, which is fine for
        // a log or a support ticket. What the user reads is decided by the client, which
        // owns the pt-BR copy. The only messages promoted to user-facing text are the ones
        // deliberately authored as such.
        if (exception is Flow.Application.Assistant.AssistantUnavailableException)
            problem.Extensions["userMessage"] = exception.Message;

        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        problem.Extensions["traceId"] = traceId;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
