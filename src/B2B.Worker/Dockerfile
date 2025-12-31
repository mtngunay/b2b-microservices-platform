# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for restore (layer caching optimization)
COPY ["src/B2B.Worker/B2B.Worker.csproj", "B2B.Worker/"]
COPY ["src/B2B.Application/B2B.Application.csproj", "B2B.Application/"]
COPY ["src/B2B.Domain/B2B.Domain.csproj", "B2B.Domain/"]
COPY ["src/B2B.Infrastructure/B2B.Infrastructure.csproj", "B2B.Infrastructure/"]
COPY ["src/B2B.Shared/B2B.Shared.csproj", "B2B.Shared/"]

# Restore dependencies
RUN dotnet restore "B2B.Worker/B2B.Worker.csproj"

# Copy source code
COPY src/ .

# Build and publish
RUN dotnet publish "B2B.Worker/B2B.Worker.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Set environment variables for Hangfire worker
ENV ASPNETCORE_URLS=http://+:8081
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV HANGFIRE_WORKER_COUNT=5

# Expose port for Hangfire dashboard
EXPOSE 8081

# Copy published application
COPY --from=build /app/publish .

# Configure non-root user for security
# The 'app' user is pre-created in the aspnet base image
USER app

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8081/health/live || exit 1

# Entry point
ENTRYPOINT ["dotnet", "B2B.Worker.dll"]
