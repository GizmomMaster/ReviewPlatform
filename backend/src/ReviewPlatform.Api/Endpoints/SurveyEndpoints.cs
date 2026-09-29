using MediatR;
using ReviewPlatform.Api.Infrastructure;
using ReviewPlatform.Application.Surveys;

namespace ReviewPlatform.Api.Endpoints;

public sealed record SurveyAnswersRequest(IReadOnlyList<SurveyAnswerDto> Answers);

/// <summary>Публичная анкета: доступ только по токену из персональной ссылки.</summary>
internal static class SurveyEndpoints
{
    public static IEndpointRouteBuilder MapSurveyEndpoints(this IEndpointRouteBuilder app)
    {
        var surveys = app.MapGroup("/api/surveys/{token}")
            .WithTags("Surveys")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimits.Survey);

        surveys.MapGet("/", async (string token, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetSurveyQuery(token), ct)))
            .WithName("GetSurvey");

        surveys.MapPut("/draft", async (string token, SurveyAnswersRequest request, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SaveSurveyDraftCommand(token, request.Answers ?? []), ct);
            return TypedResults.NoContent();
        })
            .WithName("SaveSurveyDraft");

        surveys.MapPost("/submit", async (string token, SurveyAnswersRequest request, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SubmitSurveyCommand(token, request.Answers ?? []), ct);
            return TypedResults.NoContent();
        })
            .WithName("SubmitSurvey");

        return app;
    }
}
