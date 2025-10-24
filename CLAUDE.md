# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Memorizer Self-Contained** is a C# .NET 9 implementation of a memory service with embedded vector database and LLM capabilities. It runs entirely self-contained in a single Docker container with NO external dependencies - all AI models (~685MB) are bundled in the repository.

Based on the original [Memorizer](https://github.com/petabridge/memorizer) by Dario Griffo and Petabridge, LLC (MIT License).

## Architecture

- **Language:** C# / .NET 9.0
- **Vector Database:** SQLite with manual cosine similarity (no extensions needed)
- **Embeddings:** LLamaSharp with all-MiniLM-L6-v2 GGUF model (384 dimensions)
- **LLM:** LLamaSharp with TinyLlama 1.1B GGUF model
- **Web Framework:** ASP.NET Core Minimal APIs
- **MCP Server:** ModelContextProtocol.AspNetCore (exposed at `/mcp`)
- **Models Location:** `/models/` directory (~685MB total, committed to git)

### Key Design Decisions

1. **Manual Vector Search:** Uses brute-force cosine similarity in C# rather than native SQLite extensions for maximum portability
2. **Air-Gapped Ready:** All models are committed to git and bundled in Docker image
3. **Dual Embeddings:** Stores both content embeddings and metadata embeddings (type + tags) for better search
4. **Fallback Title Generation:** If LLM fails to generate title, falls back to truncated content

## Development Commands

### Local Development

```bash
# Navigate to project directory
cd src/Memorizer.Self

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run locally (requires models in ../../models/)
dotnet run

# Access locally
# API: http://localhost:8000/api/memories
# Swagger: http://localhost:8000/swagger
# Health: http://localhost:8000/healthz
```

### Docker Deployment

```bash
# Build Docker image (models must be in ./models/ directory)
docker-compose build

# Run container
docker-compose up -d

# View logs
docker-compose logs -f memorizer-self

# Check health
curl http://localhost:9000/healthz

# Stop
docker-compose down
```

### Testing the API

```bash
# Store a memory
curl -X POST http://localhost:9000/api/memories \
  -H "Content-Type: application/json" \
  -d '{
    "type": "note",
    "content": {"text": "Your memory content here"},
    "source": "user",
    "tags": ["test", "example"]
  }'

# Search memories
curl -X POST http://localhost:9000/api/memories/search \
  -H "Content-Type: application/json" \
  -d '{"query": "your search query", "limit": 10}'

# Get statistics
curl http://localhost:9000/api/memories/stats
```

## Project Structure

```
/
├── src/Memorizer.Self/               # Main application
│   ├── Controllers/                  # REST API endpoints
│   │   └── MemoriesController.cs     # Memory CRUD operations
│   ├── Data/                         # Database layer
│   │   └── SqliteVectorDb.cs         # SQLite with manual vector search
│   ├── Services/                     # Business logic
│   │   ├── IEmbeddingService.cs      # Embedding interface
│   │   ├── LLamaEmbeddingService.cs  # LLamaSharp embeddings
│   │   ├── ILLMService.cs            # LLM interface
│   │   ├── LLamaService.cs           # LLamaSharp LLM
│   │   └── MemoryService.cs          # Core memory operations
│   ├── Tools/                        # MCP tools
│   │   └── MemoryTools.cs            # MCP server tools (store, search, etc.)
│   ├── Models/                       # Data models
│   │   ├── Memory.cs                 # Core memory model
│   │   ├── MemoryRelationship.cs     # Memory relationships
│   │   ├── RelationshipType.cs       # Relationship types enum
│   │   └── MemoryRequest.cs          # API request DTOs
│   ├── Program.cs                    # Application entry point
│   └── Memorizer.Self.csproj         # Project file
├── models/                           # AI models (committed to git)
│   ├── all-MiniLM-L6-v2.gguf         # 44MB - Embedding model
│   └── tinyllama-1.1b-chat.gguf      # 638MB - LLM model
├── Dockerfile                        # Multi-stage Docker build
├── docker-compose.yml                # Container orchestration
├── README.md                         # Comprehensive documentation
├── IMPLEMENTATION_GUIDE.md           # Detailed implementation steps
└── QUICKSTART.md                     # Quick start guide
```

## Key Files to Understand

1. **[Program.cs](src/Memorizer.Self/Program.cs)** - Application startup, service registration, MCP server configuration
2. **[SqliteVectorDb.cs](src/Memorizer.Self/Data/SqliteVectorDb.cs)** - Manual cosine similarity implementation for vector search
3. **[MemoryService.cs](src/Memorizer.Self/Services/MemoryService.cs)** - Core business logic with dual embedding search strategy
4. **[LLamaEmbeddingService.cs](src/Memorizer.Self/Services/LLamaEmbeddingService.cs)** - LLamaSharp integration for embeddings
5. **[MemoriesController.cs](src/Memorizer.Self/Controllers/MemoriesController.cs)** - REST API endpoints

## Important Implementation Details

### Vector Search Strategy

The manual vector search in SqliteVectorDb.cs:
- Stores embeddings as BLOB (byte arrays converted from float[])
- Calculates cosine similarity in C# for all stored vectors
- Filters results by minimum similarity threshold
- Uses tag pre-filtering to reduce search space for large databases

**Performance Note:** Manual search becomes slower with >10K memories. Consider tag filtering or implementing sqlite-vec extension for large datasets.

### Dual Embedding Search

MemoryService uses a sophisticated search strategy:
1. Primary search with content embeddings
2. If insufficient results, fallback to metadata embeddings (type + tags)
3. If still no results, retry with lower similarity threshold (min 0.5)
4. Loads relationships for all returned memories

### Model Loading

- Models are loaded as singletons during application startup
- LLamaSharp 0.18.0 is used (newer versions may have conflicts)
- CPU-only mode (GpuLayerCount = 0) for maximum portability
- Startup time: 60-90 seconds due to model loading

## Configuration

Environment variables (set in docker-compose.yml or appsettings.json):

```bash
# Models path (default: ./models)
ModelsPath=/app/models

# Database and data path (default: ./data)
DataPath=/app/data

# ASP.NET Core
ASPNETCORE_URLS=http://+:8000
ASPNETCORE_ENVIRONMENT=Production
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | API information |
| GET | `/healthz` | Health check |
| GET | `/swagger` | API documentation |
| POST | `/api/memories` | Create memory |
| GET | `/api/memories/{id}` | Get memory by ID |
| GET | `/api/memories?ids=...` | Get multiple memories |
| POST | `/api/memories/search` | Search memories |
| DELETE | `/api/memories/{id}` | Delete memory |
| POST | `/api/memories/relationships` | Create relationship |
| GET | `/api/memories/stats` | Get database statistics |
| * | `/mcp` | MCP server endpoint |

## MCP Tools (Available at `/mcp`)

Six tools exposed via Model Context Protocol:
1. **store** - Store new memories with automatic embedding generation
2. **search** - Semantic search with configurable similarity threshold
3. **get** - Retrieve specific memory by ID
4. **get_many** - Fetch multiple memories by IDs
5. **delete** - Remove memories
6. **create_relationship** - Link memories with typed relationships

## Common Tasks

### Adding a New API Endpoint

1. Add method to [MemoriesController.cs](src/Memorizer.Self/Controllers/MemoriesController.cs)
2. If new business logic needed, add to [MemoryService.cs](src/Memorizer.Self/Services/MemoryService.cs)
3. If database operations needed, add to [SqliteVectorDb.cs](src/Memorizer.Self/Data/SqliteVectorDb.cs)

### Adding a New MCP Tool

Add tool method to [MemoryTools.cs](src/Memorizer.Self/Tools/MemoryTools.cs) with `[Tool]` attribute

### Modifying Vector Search

Edit the SearchBySimilarity method in [SqliteVectorDb.cs](src/Memorizer.Self/Data/SqliteVectorDb.cs)

### Updating Models

Replace files in `/models/` directory and rebuild Docker image

## Dependencies

Key NuGet packages:
- `LLamaSharp` 0.18.0 - .NET bindings for llama.cpp
- `LLamaSharp.Backend.Cpu` 0.18.0 - CPU backend for LLamaSharp
- `Microsoft.Data.Sqlite` 9.0.0 - SQLite ADO.NET provider
- `ModelContextProtocol.AspNetCore` 0.4.0-preview.2 - MCP server
- `Serilog.AspNetCore` 8.0.3 - Logging

## Performance Characteristics

- **Startup Time:** 60-90 seconds (model loading)
- **Memory Usage:** 2-4GB RAM
- **Docker Image Size:** ~2GB (includes models)
- **Embedding Generation:** ~100ms per text
- **LLM Title Generation:** ~2-5 seconds (CPU)
- **Vector Search:** 50-500ms (depends on database size)

## Deployment

### Air-Gapped Deployment

```bash
# On internet-connected machine
docker-compose build
docker save memorizer-self:latest | gzip > memorizer-self.tar.gz

# Transfer memorizer-self.tar.gz to air-gapped machine

# On air-gapped machine
docker load < memorizer-self.tar.gz
docker run -d -p 9000:8000 -v memorizer-data:/app/data memorizer-self:latest
```

### Platform Notes

- Docker build uses `platform: linux/amd64` - LLamaSharp ARM64 Linux binaries not yet available
- For ARM64 (e.g., Apple Silicon), Docker will use Rosetta translation (slower)
- Native ARM64 support requires building LLamaSharp from source

## Troubleshooting

### Models Not Loading
```bash
# Verify models exist
docker exec memorizer-self ls -lh /app/models
# Should show: all-MiniLM-L6-v2.gguf (44MB) and tinyllama-1.1b-chat.gguf (638MB)
```

### Slow Performance
- Allocate more CPU cores (edit docker-compose.yml resources)
- Use tag filtering in search queries to reduce search space
- Consider GPU support (requires nvidia-docker and GPU-enabled models)

### Database Issues
```bash
# Check database file
docker exec memorizer-self sqlite3 /app/data/memorizer.db "SELECT COUNT(*) FROM memories;"
```

## License

MIT License - Same as original Memorizer project
