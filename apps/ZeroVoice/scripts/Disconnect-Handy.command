#!/bin/bash
set -euo pipefail
package_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
settings_file="$HOME/Library/Application Support/com.pais.handy/settings_store.json"
"$package_dir/ZeroVoice.app/Contents/MacOS/ZeroVoice" --disconnect-handy "$settings_file"
echo '変更した5項目を復旧しました。'
