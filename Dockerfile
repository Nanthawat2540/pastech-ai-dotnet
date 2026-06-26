FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files first for layer caching
COPY src/PasTechAI.Domain/PasTechAI.Domain.csproj         src/PasTechAI.Domain/
COPY src/PasTechAI.Application/PasTechAI.Application.csproj src/PasTechAI.Application/
COPY src/PasTechAI.Infrastructure/PasTechAI.Infrastructure.csproj src/PasTechAI.Infrastructure/
COPY src/PasTechAI.API/PasTechAI.API.csproj               src/PasTechAI.API/

RUN dotnet restore src/PasTechAI.API/PasTechAI.API.csproj

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/PasTechAI.API/PasTechAI.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "PasTechAI.API.dll"]
