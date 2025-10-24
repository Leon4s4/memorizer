#!/bin/bash

# Download models script for Memorizer Self-Contained
# Run this ONCE on a machine with internet access

set -e

MODELS_DIR="models"
mkdir -p "$MODELS_DIR"

echo "📦 Downloading models for Memorizer Self-Contained..."
echo "This will download ~700MB of models"
echo ""

# Embedding model (~90MB)
if [ -f "$MODELS_DIR/all-MiniLM-L6-v2.gguf" ]; then
    echo "✅ Embedding model already exists, skipping..."
else
    echo "⬇️  Downloading embedding model (all-MiniLM-L6-v2)..."
    wget --progress=bar:force \
      https://huggingface.co/TheBloke/all-MiniLM-L6-v2-GGUF/resolve/main/all-minilm-l6-v2.Q4_K_M.gguf \
      -O "$MODELS_DIR/all-MiniLM-L6-v2.gguf"
    echo "✅ Embedding model downloaded"
fi

# LLM model (~600MB)
if [ -f "$MODELS_DIR/tinyllama-1.1b-chat.gguf" ]; then
    echo "✅ LLM model already exists, skipping..."
else
    echo "⬇️  Downloading LLM model (TinyLlama 1.1B)..."
    wget --progress=bar:force \
      https://huggingface.co/TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF/resolve/main/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf \
      -O "$MODELS_DIR/tinyllama-1.1b-chat.gguf"
    echo "✅ LLM model downloaded"
fi

echo ""
echo "✅ All models downloaded successfully!"
echo ""
echo "📊 Model sizes:"
ls -lh "$MODELS_DIR/"
echo ""
echo "🐳 You can now build the Docker image without internet:"
echo "   docker-compose build"
