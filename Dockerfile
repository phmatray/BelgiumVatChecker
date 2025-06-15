# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["BelgiumVatChecker.sln", "./"]
COPY ["BelgiumVatChecker.Core/BelgiumVatChecker.Core.csproj", "BelgiumVatChecker.Core/"]
COPY ["BelgiumVatChecker.Api/BelgiumVatChecker.Api.csproj", "BelgiumVatChecker.Api/"]
COPY ["BelgiumVatChecker.Tests/BelgiumVatChecker.Tests.csproj", "BelgiumVatChecker.Tests/"]

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY . .

# Build the application
RUN dotnet build -c Release --no-restore

# Run tests
RUN dotnet test -c Release --no-build --verbosity normal

# Publish the API
RUN dotnet publish "BelgiumVatChecker.Api/BelgiumVatChecker.Api.csproj" -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published application
COPY --from=build /app/publish .

# Change ownership of app directory
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Set environment variables
ENV ASPNETCORE_URLS="http://+:8080;https://+:8081"
ENV ASPNETCORE_ENVIRONMENT="Production"

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/api/vat/status || exit 1

# Start the application
ENTRYPOINT ["dotnet", "BelgiumVatChecker.Api.dll"]