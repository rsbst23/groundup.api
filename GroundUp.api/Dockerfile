# Use ASP.NET 8.0 base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use .NET SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy only the main application and dependencies (EXCLUDING TEST PROJECTS)
COPY ["GroundUp.api/GroundUp.api.csproj", "GroundUp.api/"]
COPY ["GroundUp.core/GroundUp.core.csproj", "GroundUp.core/"]
COPY ["GroundUp.infrastructure/GroundUp.infrastructure.csproj", "GroundUp.infrastructure/"]

# Restore only the main API project (not test projects)
WORKDIR /src/GroundUp.api
RUN dotnet restore "./GroundUp.api.csproj"

# Copy the remaining application source code
COPY . .

# Build the API project (EXCLUDES TEST PROJECTS)
WORKDIR "/src/GroundUp.api"
RUN dotnet build "./GroundUp.api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GroundUp.api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM base AS final
WORKDIR /app

# Copy published output
COPY --from=publish /app/publish .

# Copy startup script
COPY ["GroundUp.api/startup.sh", "/app/startup.sh"]

USER root
RUN chmod +x /app/startup.sh
USER app

# Use `startup.sh` as the entrypoint instead of directly running the .NET app
ENTRYPOINT ["/app/startup.sh"]
