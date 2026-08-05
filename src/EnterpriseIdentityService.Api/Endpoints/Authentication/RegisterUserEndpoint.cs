using EnterpriseIdentityService.Api.Extensions;
using EnterpriseIdentityService.Application.Users.Register;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseIdentityService.Api.Endpoints.Authentication;

internal static class RegisterUserEndpoint
{
    public static IEndpointRouteBuilder MapRegisterUserEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/auth/register",
                HandleAsync)
            .WithName("RegisterUser")
            .WithTags("Authentication")
            .Produces<RegisterUserResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        RegisterUserRequest request,
        RegisterUserCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(
            request.Email,
            request.Username,
            request.Password);

        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created(
                $"/api/users/{result.Value.Value}",
                new RegisterUserResponse(result.Value.Value))
            : result.ToProblem();
    }
}
