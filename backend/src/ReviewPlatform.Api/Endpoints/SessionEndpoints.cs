using MediatR;
using ReviewPlatform.Application.Reports;
using ReviewPlatform.Application.Sessions;
using ReviewPlatform.Domain.Assessments;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Api.Endpoints;

public sealed record UpdateSessionRequest(SessionType Type, DateTime DeadlineAtUtc);

public sealed record AddParticipantRequest(string FullName, string Email, EvaluatorRole Role);

public sealed record ExtendSessionRequest(DateTime NewDeadlineAtUtc);

public sealed record DecideRequest(DecisionOutcome Outcome, Guid NewGradeId, string Comment, IReadOnlyList<PlanItemDto> PlanItems);

internal static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var sessions = app.MapGroup("/api/assessment-sessions").WithTags("Assessment sessions");

        sessions.MapGet("/", async (SessionStatus? status, Guid? employeeId, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new ListSessionsQuery(status, employeeId), ct)))
            .WithName("ListSessions");

        sessions.MapPost("/", async (CreateSessionCommand command, ISender sender, CancellationToken ct) =>
        {
            var session = await sender.Send(command, ct);
            return TypedResults.Created($"/api/assessment-sessions/{session.Id}", session);
        })
            .WithName("CreateSession");

        sessions.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetSessionQuery(id), ct)))
            .WithName("GetSession");

        sessions.MapPut("/{id:guid}", async (Guid id, UpdateSessionRequest r, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new UpdateSessionCommand(id, r.Type, r.DeadlineAtUtc), ct)))
            .WithName("UpdateSession");

        sessions.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteSessionCommand(id), ct);
            return TypedResults.NoContent();
        })
            .WithName("DeleteSession");

        sessions.MapGet("/{id:guid}/survey-preview", async (Guid id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetSurveyPreviewQuery(id), ct)))
            .WithName("GetSurveyPreview");

        sessions.MapPost("/{id:guid}/launch", async (Guid id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new LaunchSessionCommand(id), ct)))
            .WithName("LaunchSession");

        sessions.MapPost("/{id:guid}/cancel", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CancelSessionCommand(id), ct);
            return TypedResults.NoContent();
        })
            .WithName("CancelSession");

        sessions.MapPost("/{id:guid}/participants", async (Guid id, AddParticipantRequest r, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new AddParticipantCommand(id, r.FullName, r.Email, r.Role), ct)))
            .WithName("AddParticipant");

        sessions.MapDelete("/{id:guid}/participants/{participantId:guid}", async (Guid id, Guid participantId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new RemoveParticipantCommand(id, participantId), ct);
            return TypedResults.NoContent();
        })
            .WithName("RemoveParticipant");

        sessions.MapPost("/{id:guid}/participants/{participantId:guid}/reissue-link", async (Guid id, Guid participantId, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new ReissueLinkCommand(id, participantId), ct)))
            .WithName("ReissueParticipantLink");

        sessions.MapPost("/{id:guid}/participants/{participantId:guid}/resend-invite", async (Guid id, Guid participantId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ResendInviteCommand(id, participantId), ct);
            return TypedResults.NoContent();
        })
            .WithName("ResendParticipantInvite");

        sessions.MapPost("/{id:guid}/extend", async (Guid id, ExtendSessionRequest r, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new ExtendSessionCommand(id, r.NewDeadlineAtUtc), ct)))
            .WithName("ExtendSession");

        sessions.MapPost("/{id:guid}/close-early", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CloseSessionEarlyCommand(id), ct);
            return TypedResults.NoContent();
        })
            .WithName("CloseSessionEarly");

        sessions.MapPost("/{id:guid}/decision", async (Guid id, DecideRequest r, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new DecideSessionCommand(id, r.Outcome, r.NewGradeId, r.Comment, r.PlanItems ?? []), ct)))
            .WithName("DecideSession");

        sessions.MapGet("/{id:guid}/audit", async (Guid id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetSessionAuditQuery(id), ct)))
            .WithName("GetSessionAudit");

        sessions.MapGet("/{id:guid}/report", async (Guid id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetSessionReportQuery(id), ct)))
            .WithName("GetSessionReport");

        sessions.MapGet("/{id:guid}/report/export", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var file = await sender.Send(new ExportSessionReportQuery(id), ct);
            return TypedResults.File(file.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.FileName);
        })
            .WithName("ExportSessionReport");

        return app;
    }
}
