# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/LocalHub.Domain/LocalHub.Domain.csproj src/LocalHub.Domain/
COPY src/LocalHub.Application/LocalHub.Application.csproj src/LocalHub.Application/
COPY src/LocalHub.Infrastructure/LocalHub.Infrastructure.csproj src/LocalHub.Infrastructure/
COPY src/LocalHub.Api/LocalHub.Api.csproj src/LocalHub.Api/
RUN dotnet restore src/LocalHub.Api/LocalHub.Api.csproj

COPY src/ src/
RUN dotnet publish src/LocalHub.Api/LocalHub.Api.csproj -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LocalHub.Api.dll"]
