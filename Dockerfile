# syntax=docker/dockerfile:1.7

# Multi-stage build: restore + publish on the SDK image, run on the slim ASP.NET runtime.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first so `dotnet restore` caches when only source changes.
COPY src/RecruiterAi.Domain/RecruiterAi.Domain.csproj         src/RecruiterAi.Domain/
COPY src/RecruiterAi.Infrastructure/RecruiterAi.Infrastructure.csproj src/RecruiterAi.Infrastructure/
COPY src/RecruiterAi.Api/RecruiterAi.Api.csproj               src/RecruiterAi.Api/
RUN dotnet restore src/RecruiterAi.Api/RecruiterAi.Api.csproj

COPY src/ src/
RUN dotnet publish src/RecruiterAi.Api/RecruiterAi.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Uploads volume mount point — overridden via Storage__UploadsPath env var.
RUN mkdir -p /app/uploads
VOLUME ["/app/uploads"]

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    Storage__UploadsPath=/app/uploads
EXPOSE 8080

ENTRYPOINT ["dotnet", "RecruiterAi.Api.dll"]
