#!/bin/bash
set -euo pipefail
package_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
model_dir="$package_dir/ZeroVoice.app/Contents/MacOS/models"
mkdir -p "$model_dir"
model_file="$model_dir/Qwen_Qwen3-4B-Instruct-2507-Q4_K_M.gguf"
expected='2fde00ce69dd4899c70d020845e2638353015bba0fdf161b3eb965f2bca4464e'
if [[ ! -f "$model_file" ]]; then
  /usr/bin/curl --fail --location --proto '=https' --proto-redir '=https' --tlsv1.2 'https://huggingface.co/bartowski/Qwen_Qwen3-4B-Instruct-2507-GGUF/resolve/ae44f08e1392f39c0e474af10c3ff8355c8b6688/Qwen_Qwen3-4B-Instruct-2507-Q4_K_M.gguf' -o "$model_file.download"
  actual="$(/usr/bin/shasum -a 256 "$model_file.download" | /usr/bin/awk '{print $1}')"
  [[ "$actual" == "$expected" ]] || { echo 'Model hash mismatch'; exit 1; }
  /bin/mv "$model_file.download" "$model_file"
fi
actual="$(/usr/bin/shasum -a 256 "$model_file" | /usr/bin/awk '{print $1}')"
[[ "$actual" == "$expected" ]] || { echo 'Model hash mismatch'; exit 1; }
echo 'ローカルモデルを検証しました。ZeroVoice.app を起動できます。'
