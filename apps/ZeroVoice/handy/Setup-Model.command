#!/bin/bash
set -euo pipefail
MODEL_DIR="$HOME/Library/Application Support/Handy-ZeroVoice/models"
MODEL="$MODEL_DIR/gemma-4-E2B_q4_0-it.gguf"
SHA="fa401b55b07ee70a54c6dae3903c783a6e65064312529ea57175cb5f8dec6634"
mkdir -p "$MODEL_DIR"
if [[ -f "$MODEL" ]] && [[ "$(shasum -a 256 "$MODEL" | cut -d ' ' -f 1)" == "$SHA" ]]; then
  echo 'Gemma 4 is already verified. Open Handy.'
  exit 0
fi
TMP="$(mktemp "$MODEL_DIR/download.XXXXXX")"
trap 'rm -f "$TMP"' EXIT
curl --fail --location --proto '=https' --tlsv1.2 --output "$TMP" 'https://huggingface.co/google/gemma-4-E2B-it-qat-q4_0-gguf/resolve/675cff42a74c774d6cb76f76d8eacb49b48c9b93/gemma-4-E2B_q4_0-it.gguf'
[[ "$(shasum -a 256 "$TMP" | cut -d ' ' -f 1)" == "$SHA" ]] || { echo 'Checksum mismatch'; exit 1; }
mv "$TMP" "$MODEL"
echo 'Gemma 4 is verified. Open Handy.'
