# Use ASP.NET 8.0 base image for runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use .NET SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy only the project files needed for restore/publish
COPY ["GroundUp.Sample/GroundUp.Sample.csproj", "GroundUp.Sample/"]
COPY ["GroundUp.Api/GroundUp.Api.csproj", "GroundUp.Api/"]
COPY ["GroundUp.Core/GroundUp.Core.csproj", "GroundUp.Core/"]
COPY ["GroundUp.Data.Abstractions/GroundUp.Data.Abstractions.csproj", "GroundUp.Data.Abstractions/"]
COPY ["GroundUp.Data.Core/GroundUp.Data.Core.csproj", "GroundUp.Data.Core/"]
COPY ["GroundUp.Repositories.Inventory/GroundUp.Repositories.Inventory.csproj", "GroundUp.Repositories.Inventory/"]
COPY ["GroundUp.Services.Core/GroundUp.Services.Core.csproj", "GroundUp.Services.Core/"]
COPY ["GroundUp.Services.Inventory/GroundUp.Services.Inventory.csproj", "GroundUp.Services.Inventory/"]

# Restore dependencies
RUN dotnet restore "/src/GroundUp.Sample/GroundUp.Sample.csproj" --disable-parallel --ignore-failed-sources

# Copy the entire source (still remove tests to keep image lean)
COPY . .
RUN rm -rf /src/GroundUp.Api.Tests.Integration \
           /src/GroundUp.Api.Tests.Unit

# Build & Publish the runnable host (composition root)
RUN dotnet publish "/src/GroundUp.Sample/GroundUp.Sample.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM base AS final
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Copy startup script (hosted in Sample)
COPY ["GroundUp.Sample/startup.sh", "/app/startup.sh"]

USER root
RUN chmod +x /app/startup.sh
USER app

ENTRYPOINT ["/app/startup.sh"]
