using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using ReviewPlatform.Application.Matrix;

namespace ReviewPlatform.Api.Endpoints;

internal static class MatrixEndpoints
{
    public static IEndpointRouteBuilder MapMatrixEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Matrix");

        api.MapGet("/tracks", async (ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetTracksQuery(), ct)))
            .WithName("GetTracks");

        api.MapGet("/grades", async (ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetGradesQuery(), ct)))
            .WithName("GetGrades");

        api.MapGet("/tracks/{trackId:guid}/matrix", async Task<Results<Ok<MatrixDto>, NotFound>> (Guid trackId, ISender sender, CancellationToken ct) =>
            await sender.Send(new GetMatrixQuery(trackId), ct) is { } matrix ? TypedResults.Ok(matrix) : TypedResults.NotFound())
            .WithName("GetMatrix");

        api.MapGet("/grade-role-rules", async (ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetGradeRoleRulesQuery(), ct)))
            .WithName("GetGradeRoleRules");

        return app;
    }
}
