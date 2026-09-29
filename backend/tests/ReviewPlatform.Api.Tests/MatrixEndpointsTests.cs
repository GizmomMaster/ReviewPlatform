using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReviewPlatform.Application.Matrix;
using ReviewPlatform.Infrastructure;
using ReviewPlatform.Infrastructure.Persistence;

namespace ReviewPlatform.Api.Tests;

public sealed class MatrixEndpointsTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetGrades_ReturnsEightGradesInOrder()
    {
        using var client = factory.CreateClient();

        var grades = await client.GetFromJsonAsync<List<GradeDto>>("/api/grades", Json, Ct);

        Assert.NotNull(grades);
        Assert.Equal(["E1", "E2", "E3", "E4", "E5", "E6", "E7", "E8"], grades.Select(g => g.Code));
        Assert.Equal("Lead", grades[^1].Name);
    }

    [Fact]
    public async Task GetMatrix_ReturnsSeededBackendMatrix()
    {
        using var client = factory.CreateClient();
        var track = Assert.Single((await client.GetFromJsonAsync<List<TrackDto>>("/api/tracks", Json, Ct))!);

        var matrix = await client.GetFromJsonAsync<MatrixDto>($"/api/tracks/{track.Id}/matrix", Json, Ct);

        Assert.NotNull(matrix);
        Assert.Equal("backend", matrix.Track.Code);
        Assert.Equal(
            ["Экспертность", "Инженерная культура", "Ответственность за результат", "Коммуникация", "Agile Mindset", "Ориентация на бизнес", "Обучение себя и других"],
            matrix.Groups.Select(g => g.Name));
        Assert.Equal(160, matrix.Groups.Sum(g => g.Indicators.Count));

        // Индикаторы внутри группы отсортированы по грейду
        var gradeOrder = matrix.Grades.ToDictionary(g => g.Id, g => g.Order);
        Assert.All(matrix.Groups, g => Assert.Equal(g.Indicators.OrderBy(i => gradeOrder[i.GradeId]).ThenBy(i => i.Order), g.Indicators));
    }

    [Fact]
    public async Task GetMatrix_UnknownTrack_Returns404()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/tracks/{Guid.NewGuid()}/matrix", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InitializeDatabase_RunTwice_DoesNotDuplicateData()
    {
        await factory.Services.InitializeDatabaseAsync(Ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(8, await db.Grades.CountAsync(Ct));
        Assert.Equal(1, await db.Tracks.CountAsync(Ct));
        Assert.Equal(7, await db.CompetencyGroups.CountAsync(Ct));
        Assert.Equal(160, await db.Indicators.CountAsync(Ct));
        Assert.Equal(6 * 4 + 5 + 4, await db.GradeRoleRules.CountAsync(Ct));
    }
}
