# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only the csproj first so Docker can cache the restore layer
COPY PROG7313_TechMove/PROG7313_TechMove.csproj PROG7313_TechMove/
RUN dotnet restore PROG7313_TechMove/PROG7313_TechMove.csproj

# Copy the rest of the MVC source and publish
COPY PROG7313_TechMove/ PROG7313_TechMove/
RUN dotnet publish PROG7313_TechMove/PROG7313_TechMove.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 80
ENTRYPOINT ["dotnet", "PROG7313_TechMove.dll"] 
