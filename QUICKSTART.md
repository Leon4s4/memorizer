# Quick Start Guide

## ✅ Models Included - Zero Setup Required!

The AI models (~685MB) are **already committed to this repository**. No download step needed!

## 1. Build

```bash
cd memorizer-self
docker-compose build
```

**That's it!** Models are copied from `models/` directory into the image.

## 2. Run

```bash
docker-compose up -d
```

## 3. Access

- **API:** http://localhost:9000/api/memories
- **Swagger:** http://localhost:9000/swagger
- **Health:** http://localhost:9000/healthz

## Test It

```bash
# Store a memory
curl -X POST http://localhost:9000/api/memories \
  -H "Content-Type: application/json" \
  -d '{
    "type": "note",
    "content": {"text": "C# Self-Contained works!"},
    "source": "test",
    "tags": ["test"]
  }'

# Search
curl -X POST http://localhost:9000/api/memories/search \
  -H "Content-Type: application/json" \
  -d '{"query": "C# works", "limit": 5}'
```

## What's Included

| File | Size | Purpose |
|------|------|---------|
| `all-MiniLM-L6-v2.gguf` | 44MB | Embedding model (text → vectors) |
| `tinyllama-1.1b-chat.gguf` | 638MB | LLM model (title generation) |

**Total:** ~685MB committed to git

## Air-Gapped Deployment

```bash
# Build image
docker-compose build

# Save to file
docker save memorizer-self:latest | gzip > memorizer-self.tar.gz

# Transfer memorizer-self.tar.gz to air-gapped machine

# Load and run
docker load < memorizer-self.tar.gz
docker run -d -p 9000:8000 memorizer-self:latest
```

## Why Models in Git?

✅ **No separate download** - Clone and build
✅ **Air-gapped friendly** - Everything included
✅ **Reproducible** - Same models every time
✅ **Simple** - One command to build

**Trade-off:** Repository is ~690MB (worth it for simplicity!)

## Need Help?

- Full guide: [README.md](README.md)
- Model details: [MODELS.md](MODELS.md)
- Implementation: [IMPLEMENTATION_GUIDE.md](IMPLEMENTATION_GUIDE.md)

---

**Version:** 2.0.0 - Self-Contained C# with Embedded Models
