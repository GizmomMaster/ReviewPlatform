using MediatR;
using ReviewPlatform.Api.Auth;
using ReviewPlatform.Application.Users;

namespace ReviewPlatform.Api.Endpoints;

public sealed record UpdateUserRequest(string FullName, string Role, bool IsActive);

public sealed record ResetPasswordRequest(string NewPassword);

internal static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization(Policies.Admin);

        users.MapGet("/", async (ISender sender, CancellationToken ct) => TypedResults.Ok(await sender.Send(new ListUsersQuery(), ct)))
            .WithName("ListUsers");

        users.MapPost("/", async (CreateUserCommand command, ISender sender, CancellationToken ct) =>
        {
            var user = await sender.Send(command, ct);
            return TypedResults.Created($"/api/users/{user.Id}", user);
        })
            .WithName("CreateUser");

        users.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new UpdateUserCommand(id, request.FullName, request.Role, request.IsActive), ct)))
            .WithName("UpdateUser");

        users.MapPost("/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest request, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ResetPasswordCommand(id, request.NewPassword), ct);
            return TypedResults.NoContent();
        })
            .WithName("ResetUserPassword");

        return app;
    }
}
