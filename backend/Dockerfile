# Контекст сборки — корень репозитория (нужен общий .editorconfig)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY .editorconfig ./
COPY backend/global.json backend/Directory.Build.props backend/Directory.Packages.props backend/
COPY backend/src/ReviewPlatform.Domain/*.csproj backend/src/ReviewPlatform.Domain/
COPY backend/src/ReviewPlatform.Application/*.csproj backend/src/ReviewPlatform.Application/
COPY backend/src/ReviewPlatform.Infrastructure/*.csproj backend/src/ReviewPlatform.Infrastructure/
COPY backend/src/ReviewPlatform.Api/*.csproj backend/src/ReviewPlatform.Api/
RUN dotnet restore backend/src/ReviewPlatform.Api/ReviewPlatform.Api.csproj

COPY seed/backend-matrix.xlsx seed/
COPY backend/src/ backend/src/
RUN dotnet publish backend/src/ReviewPlatform.Api/ReviewPlatform.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ReviewPlatform.Api.dll"]
