# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only the csproj first so Docker can cache the restore layer
COPY TechMove.Api/TechMove.Api.csproj TechMove.Api/
RUN dotnet restore TechMove.Api/TechMove.Api.csproj

# Copy the rest of the API source and publish
COPY TechMove.Api/ TechMove.Api/
RUN dotnet publish TechMove.Api/TechMove.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create the uploads directory the app writes PDFs into
RUN mkdir -p /app/wwwroot/uploads/agreements

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 80
ENTRYPOINT ["dotnet", "TechMove.Api.dll"]