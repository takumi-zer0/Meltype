#!/bin/bash
set -euo pipefail
package_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
settings_file="$HOME/Library/Application Support/com.pais.handy/settings_store.json"
"$package_dir/ZeroVoice.app/Contents/MacOS/ZeroVoice" --connect-handy "$settings_file"
echo 'Handyを起動して、通常の音声入力ショートカットを使ってください。'
