using MediatR;
using ReviewPlatform.Application.Employees;

namespace ReviewPlatform.Api.Endpoints;

public sealed record UpdateEmployeeRequest(string FullName, string Email, Guid TrackId, Guid GradeId, Guid? ManagerUserId, bool IsActive);

internal static class EmployeeEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder app)
    {
        var employees = app.MapGroup("/api/employees").WithTags("Employees");

        employees.MapGet("/", async (string? search, bool? includeArchived, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new ListEmployeesQuery(search, includeArchived ?? false), ct)))
            .WithName("ListEmployees");

        employees.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetEmployeeQuery(id), ct)))
            .WithName("GetEmployee");

        employees.MapPost("/", async (CreateEmployeeCommand command, ISender sender, CancellationToken ct) =>
        {
            var employee = await sender.Send(command, ct);
            return TypedResults.Created($"/api/employees/{employee.Id}", employee);
        })
            .WithName("CreateEmployee");

        employees.MapPut("/{id:guid}", async (Guid id, UpdateEmployeeRequest r, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new UpdateEmployeeCommand(id, r.FullName, r.Email, r.TrackId, r.GradeId, r.ManagerUserId, r.IsActive), ct)))
            .WithName("UpdateEmployee");

        employees.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ArchiveEmployeeCommand(id), ct);
            return TypedResults.NoContent();
        })
            .WithName("ArchiveEmployee");

        return app;
    }
}
