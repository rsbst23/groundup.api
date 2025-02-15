# Use ASP.NET 9.0 base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use .NET SDK for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files and restore dependencies
COPY ["GroundUp.api/GroundUp.api.csproj", "GroundUp.api/"]
RUN dotnet restore "./GroundUp.api/./GroundUp.api.csproj"

# Copy remaining source code
COPY . .

WORKDIR "/src/GroundUp.api"
RUN dotnet build "./GroundUp.api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GroundUp.api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final image to run the application
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
