using System.Text.Json.Serialization;
using ReviewPlatform.Api.Auth;
using ReviewPlatform.Api.Endpoints;
using ReviewPlatform.Api.Infrastructure;
using ReviewPlatform.Application;
using ReviewPlatform.Infrastructure;

const string FrontendCorsPolicy = "frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiLogging(builder.Configuration);
builder.Services.AddReverseProxySupport(builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiAuth();
builder.Services.AddApiRateLimits(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.Converters.Add(new UtcDateTimeConverter());
});
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseForwardedHeaders();
app.UseApiRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(FrontendCorsPolicy);

if (app.Environment.IsDevelopment())
{
    // Swagger UI (/swagger) поверх схемы /openapi/v1.json. До авторизации: это статические файлы,
    // а политика по умолчанию требует входа для любого запроса.
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ReviewPlatform API");
        options.DocumentTitle = "ReviewPlatform API";
        options.EnablePersistAuthorization();
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapEmployeeEndpoints();
app.MapMatrixEndpoints();
app.MapSessionEndpoints();
app.MapSurveyEndpoints();

await app.Services.InitializeDatabaseAsync();
await app.RunAsync();
