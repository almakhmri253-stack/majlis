# ── Build Stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# restore separately (cached layer)
COPY MajlisManagement.csproj .
RUN dotnet restore MajlisManagement.csproj --verbosity normal

# build
COPY . .
RUN dotnet publish MajlisManagement.csproj -c Release -o /out --no-restore --verbosity normal

# ── Runtime Stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /out .

RUN useradd --no-create-home appuser \
    && chown -R appuser:appuser /app
USER appuser

EXPOSE 7860
ENV ASPNETCORE_URLS=http://+:7860
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "MajlisManagement.dll"]
