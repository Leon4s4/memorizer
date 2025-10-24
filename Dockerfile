FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Build application first
WORKDIR /src

# Disable central package management for this build
RUN echo '<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>' > Directory.Build.props

COPY src/Memorizer.Self/Memorizer.Self.csproj .
RUN dotnet restore

COPY src/Memorizer.Self/ .
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Install native dependencies required by llama.cpp
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    libgomp1 \
    curl && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy pre-downloaded models from build context (NO INTERNET NEEDED!)
# Models must be downloaded first using: ./download-models.sh
COPY models/ /app/models/

# Verify models exist (fail fast if missing)
RUN test -f /app/models/all-MiniLM-L6-v2.gguf || \
    (echo "ERROR: Embedding model not found! Run ./download-models.sh first" && exit 1)
RUN test -f /app/models/tinyllama-1.1b-chat.gguf || \
    (echo "ERROR: LLM model not found! Run ./download-models.sh first" && exit 1)

# Copy application
COPY --from=build /app/publish .

# Create data directory
RUN mkdir -p /app/data && \
    mkdir -p /app/logs && \
    chmod -R 755 /app/data /app/logs /app/models

# Expose ports
EXPOSE 8000

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8000 \
    ModelsPath=/app/models \
    DataPath=/app/data

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8000/healthz || exit 1

ENTRYPOINT ["dotnet", "Memorizer.Self.dll"]
