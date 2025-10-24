# Model Management Guide

This document explains the AI models included in Memorizer Self-Contained.

## Models Are Committed to Git

**✅ The model files (~680MB total) ARE committed to this repository for easy deployment:**
- `all-MiniLM-L6-v2.gguf` - ~44MB (embedding model)
- `tinyllama-1.1b-chat.gguf` - ~638MB (LLM model)

**Why committed to git?**
- ✅ No separate download step needed
- ✅ Everything in one place
- ✅ Perfect for air-gapped deployment
- ✅ Clone and build - that's it!

**Trade-off:** Repository size is ~690MB, but deployment is much simpler.

## Quick Start - Models Already Included!

### Build Docker Image (No Download Needed!)

```bash
cd memorizer-self
docker-compose build
```

The Dockerfile will copy models from the `models/` directory (already in repo) into the image.

### Verify Models Are Included

```bash
docker run --rm memorizer-self:latest ls -lh /app/models
```

Should show:
```
all-MiniLM-L6-v2.gguf      (~44MB)
tinyllama-1.1b-chat.gguf   (~638MB)
```

**That's it!** No download step required.

## Option 2: Transfer Pre-Downloaded Models

If you already have the models elsewhere:

### Download URLs:

**Embedding Model (all-MiniLM-L6-v2):**
```
https://huggingface.co/TheBloke/all-MiniLM-L6-v2-GGUF/resolve/main/all-minilm-l6-v2.Q4_K_M.gguf
```

**LLM Model (TinyLlama 1.1B):**
```
https://huggingface.co/TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF/resolve/main/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf
```

### Place Models:

```bash
cd memorizer-self
mkdir -p models

# Copy or move models
cp /path/to/all-minilm-l6-v2.Q4_K_M.gguf models/all-MiniLM-L6-v2.gguf
cp /path/to/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf models/tinyllama-1.1b-chat.gguf
```

**Important:** The filenames must match exactly:
- `models/all-MiniLM-L6-v2.gguf`
- `models/tinyllama-1.1b-chat.gguf`

## Option 3: Use Different Models

You can use different GGUF models if you prefer.

### Requirements:

**Embedding Model:**
- Must be a sentence-transformer model in GGUF format
- Recommended: 384-768 dimensions
- Examples: all-MiniLM-L6-v2, all-mpnet-base-v2, BGE-small

**LLM Model:**
- Must be in GGUF format
- Recommended: 1-7B parameters for CPU
- Examples: TinyLlama, Phi-2, Mistral-7B

### Steps:

1. Download your chosen models
2. Place in `models/` directory with the correct names
3. Update `Program.cs` if using different filenames:

```csharp
// In Program.cs, update these lines:
var embeddingModelPath = Path.Combine(modelsPath, "your-embedding-model.gguf");
var llmModelPath = Path.Combine(modelsPath, "your-llm-model.gguf");
```

4. Rebuild Docker image

## Air-Gapped Deployment

For completely offline deployment:

### On Internet-Connected Machine:

```bash
# 1. Download models
./download-models.sh

# 2. Build Docker image (includes models)
docker-compose build

# 3. Save image to file
docker save memorizer-self:latest | gzip > memorizer-self.tar.gz

# 4. Transfer TWO files to air-gapped machine:
#    - memorizer-self.tar.gz (~2GB)
#    - docker-compose.yml (for easy deployment)
```

### On Air-Gapped Machine:

```bash
# 1. Load Docker image
docker load < memorizer-self.tar.gz

# 2. Run container (models already inside!)
docker run -d \
  -p 9000:8000 \
  -v memorizer-self-data:/app/data \
  --name memorizer-self \
  memorizer-self:latest
```

## Troubleshooting

### Error: "Embedding model not found!"

```bash
# Docker build fails with this error
# Solution: Download models first

./download-models.sh
```

### Error: "LLM model not found!"

```bash
# Docker build fails with this error
# Solution: Verify both models exist

ls -lh models/
# Should show both .gguf files
```

### Models Downloaded But Build Still Fails

Check filenames match exactly:

```bash
cd models
ls -1

# Should show:
# all-MiniLM-L6-v2.gguf
# tinyllama-1.1b-chat.gguf

# NOT:
# all-minilm-l6-v2.Q4_K_M.gguf  (wrong!)
```

Rename if needed:
```bash
mv all-minilm-l6-v2.Q4_K_M.gguf all-MiniLM-L6-v2.gguf
mv tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf tinyllama-1.1b-chat.gguf
```

### Want to Use Models from Another Project?

If you have models from the Python version or elsewhere:

```bash
# Copy from Python version
cp ../memorizer-python/models/*.gguf models/

# Rename to match expected names
mv models/sentence-transformer.gguf models/all-MiniLM-L6-v2.gguf
mv models/tinyllama.gguf models/tinyllama-1.1b-chat.gguf
```

## Model Storage Options

### Option A: Keep Models Out of Git (Current Setup)

**Pros:**
- ✅ Small git repo (~10MB)
- ✅ Fast git operations
- ✅ Easy to update models independently

**Cons:**
- ❌ Must download models separately
- ❌ Two-step setup (download + build)

### Option B: Commit Models to Git (Not Recommended)

You could commit models to git, but this is **NOT recommended**:

```bash
# Remove from .gitignore (NOT RECOMMENDED!)
# Edit .gitignore and remove:
# models/*.gguf
# models/*.bin

git add models/*.gguf
git commit -m "Add models (700MB)"
```

**Why not recommended:**
- ❌ Git repo becomes 700MB+
- ❌ Slow clone times
- ❌ Git is not designed for large binaries
- ❌ Hard to update models later

### Option C: Git LFS (Advanced)

Use Git Large File Storage:

```bash
# Install git-lfs
git lfs install

# Track model files
git lfs track "models/*.gguf"
git add .gitattributes
git add models/*.gguf
git commit -m "Add models via LFS"
```

**Pros:**
- ✅ Models in git but not bloating repo
- ✅ Automatic download on clone

**Cons:**
- ❌ Requires Git LFS setup
- ❌ May have bandwidth limits

## Quick Reference

| Task | Command |
|------|---------|
| Download models | `./download-models.sh` |
| Check models exist | `ls -lh models/` |
| Build with models | `docker-compose build` |
| Verify in container | `docker run --rm memorizer-self ls /app/models` |
| Save for air-gap | `docker save memorizer-self:latest \| gzip > memorizer-self.tar.gz` |

## Model Information

### all-MiniLM-L6-v2

- **Type:** Sentence transformer (embedding model)
- **Size:** ~90MB (Q4_K_M quantized)
- **Dimensions:** 384
- **Use:** Convert text to vectors for similarity search
- **Source:** https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2

### TinyLlama 1.1B

- **Type:** Large Language Model
- **Size:** ~600MB (Q4_K_M quantized)
- **Parameters:** 1.1 billion
- **Use:** Generate titles and summaries
- **Source:** https://huggingface.co/TinyLlama/TinyLlama-1.1B-Chat-v1.0

---

**Recommendation:** Use Option 1 (download script) for simplicity and keep models out of git.
