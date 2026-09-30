using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using ReviewPlatform.Api.Auth;
using ReviewPlatform.Application.Matrix;
using ReviewPlatform.Application.Reports;

namespace ReviewPlatform.Api.Endpoints;

public sealed record CreateTrackRequest(string Code, string Name);

public sealed record UpdateTrackRequest(string Name, bool IsActive);

public sealed record RenameGradeRequest(string Name);

public sealed record CreateGroupRequest(Guid TrackId, string Name, string? Description);

public sealed record UpdateGroupRequest(string Name, string? Description);

public sealed record ReorderGroupsRequest(Guid TrackId, IReadOnlyList<Guid> GroupIds);

public sealed record CreateIndicatorRequest(Guid GroupId, Guid GradeId, string Text);

public sealed record UpdateIndicatorRequest(string Text);

public sealed record ReorderIndicatorsRequest(Guid GroupId, Guid GradeId, IReadOnlyList<Guid> IndicatorIds);

internal static class MatrixEndpoints
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>С большим запасом: матрица на несколько сотен индикаторов весит десятки килобайт.</summary>
    private const long MaxImportFileBytes = 5 * 1024 * 1024;

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

        MapAdminEndpoints(api.MapGroup("").RequireAuthorization(Policies.Admin));
        return app;
    }

    private static void MapAdminEndpoints(RouteGroupBuilder admin)
    {
        // ---------- Направления и грейды ----------

        admin.MapPost("/tracks", async (CreateTrackRequest r, ISender sender, CancellationToken ct) =>
        {
            var track = await sender.Send(new CreateTrackCommand(r.Code, r.Name), ct);
            return TypedResults.Created($"/api/tracks/{track.Id}", track);
        })
            .WithName("CreateTrack");

        admin.MapPut("/tracks/{id:guid}", async (Guid id, UpdateTrackRequest r, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new UpdateTrackCommand(id, r.Name, r.IsActive), ct)))
            .WithName("UpdateTrack");

        admin.MapPut("/grades/{id:guid}", async (Guid id, RenameGradeRequest r, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new RenameGradeCommand(id, r.Name), ct)))
            .WithName("RenameGrade");

        admin.MapPut("/grade-role-rules", async (IReadOnlyList<GradeRoleRuleInput> rules, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new UpdateGradeRoleRulesCommand(rules), ct)))
            .WithName("UpdateGradeRoleRules");

        // ---------- Группы ----------

        var groups = admin.MapGroup("/competency-groups");

        groups.MapPost("/", async (CreateGroupRequest r, ISender sender, CancellationToken ct) =>
        {
            var group = await sender.Send(new CreateGroupCommand(r.TrackId, r.Name, r.Description), ct);
            return TypedResults.Created($"/api/competency-groups/{group.Id}", group);
        })
            .WithName("CreateCompetencyGroup");

        groups.MapPut("/reorder", async (ReorderGroupsRequest r, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ReorderGroupsCommand(r.TrackId, r.GroupIds), ct);
            return TypedResults.NoContent();
        })
            .WithName("ReorderCompetencyGroups");

        groups.MapPut("/{id:guid}", async (Guid id, UpdateGroupRequest r, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new UpdateGroupCommand(id, r.Name, r.Description), ct)))
            .WithName("UpdateCompetencyGroup");

        groups.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteGroupCommand(id), ct);
            return TypedResults.NoContent();
        })
            .WithName("DeleteCompetencyGroup");

        // ---------- Индикаторы ----------

        var indicators = admin.MapGroup("/indicators");

        indicators.MapPost("/", async (CreateIndicatorRequest r, ISender sender, CancellationToken ct) =>
        {
            var indicator = await sender.Send(new CreateIndicatorCommand(r.GroupId, r.GradeId, r.Text), ct);
            return TypedResults.Created($"/api/indicators/{indicator.Id}", indicator);
        })
            .WithName("CreateIndicator");

        indicators.MapPut("/reorder", async (ReorderIndicatorsRequest r, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ReorderIndicatorsCommand(r.GroupId, r.GradeId, r.IndicatorIds), ct);
            return TypedResults.NoContent();
        })
            .WithName("ReorderIndicators");

        indicators.MapPut("/{id:guid}", async (Guid id, UpdateIndicatorRequest r, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new UpdateIndicatorCommand(id, r.Text), ct)))
            .WithName("UpdateIndicator");

        indicators.MapPost("/{id:guid}/archive", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ArchiveIndicatorCommand(id), ct);
            return TypedResults.NoContent();
        })
            .WithName("ArchiveIndicator");

        indicators.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteIndicatorCommand(id), ct);
            return TypedResults.NoContent();
        })
            .WithName("DeleteIndicator");

        // ---------- Импорт и экспорт ----------

        admin.MapPost("/tracks/{trackId:guid}/matrix/import/preview", async Task<Results<Ok<MatrixImportPreviewDto>, BadRequest<string>>> (
            Guid trackId, IFormFile file, ISender sender, CancellationToken ct) =>
        {
            if (file.Length is 0 or > MaxImportFileBytes)
            {
                return TypedResults.BadRequest("Файл пустой или больше 5 МБ.");
            }

            await using var stream = file.OpenReadStream();
            return TypedResults.Ok(await sender.Send(new PreviewMatrixImportCommand(trackId, stream), ct));
        })
            .DisableAntiforgery() // API на JWT в заголовке, cookie-аутентификации нет
            .WithName("PreviewMatrixImport");

        admin.MapPost("/tracks/{trackId:guid}/matrix/import/{importId:guid}/apply", async (Guid trackId, Guid importId, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new ApplyMatrixImportCommand(trackId, importId), ct)))
            .WithName("ApplyMatrixImport");

        admin.MapGet("/tracks/{trackId:guid}/matrix/export", async (Guid trackId, ISender sender, CancellationToken ct) =>
            File(await sender.Send(new ExportMatrixQuery(trackId), ct)))
            .WithName("ExportMatrix");

        admin.MapGet("/matrix/import-template", async (ISender sender, CancellationToken ct) =>
            File(await sender.Send(new GetMatrixTemplateQuery(), ct)))
            .WithName("GetMatrixImportTemplate");
    }

    private static FileContentHttpResult File(ReportFile file) => TypedResults.File(file.Content, ExcelContentType, file.FileName);
}
