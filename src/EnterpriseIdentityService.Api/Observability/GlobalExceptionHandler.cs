using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseIdentityService.Api.Observability;

internal sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        bool malformedRequest = exception is BadHttpRequestException;
        int statusCode = malformedRequest
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;

        if (!malformedRequest)
        {
            logger.LogError(
                exception,
                "Unhandled exception while processing {RequestMethod} {RequestRoute} with trace identifier {TraceIdentifier}",
                RequestMetadata.Method(httpContext),
                RequestMetadata.Route(httpContext),
                httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = statusCode;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = malformedRequest
                    ? "The request is invalid."
                    : "An unexpected error occurred.",
                Detail = malformedRequest
                    ? "The request could not be processed."
                    : "The server could not complete the request."
            }
        });

        return true;
    }
}
