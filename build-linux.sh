#!/usr/bin/env bash
set -euo pipefail

export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT="${DOTNET_SYSTEM_GLOBALIZATION_INVARIANT:-1}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ -f "$SCRIPT_DIR/../../pnpm-workspace.yaml" ]]; then
  REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
  DEFAULT_OUTPUT_DIR="$REPO_ROOT/generated-artifacts/terraria-ultrawide-patcher-linux-x64"
else
  DEFAULT_OUTPUT_DIR="$SCRIPT_DIR/release/build-linux-x64"
fi
OUTPUT_DIR="${TERRARIA_PATCHER_OUTPUT_DIR:-$DEFAULT_OUTPUT_DIR}"

dotnet publish \
  "$SCRIPT_DIR/src/TerrariaZoomPatcher.Linux/TerrariaZoomPatcher.Linux.csproj" \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugSymbols=false \
  -p:DebugType=None \
  --output "$OUTPUT_DIR"

chmod +x "$OUTPUT_DIR/TerrariaUltrawidePatcher"
if ! command -v patchelf >/dev/null 2>&1; then
  echo "patchelf is required to remove the build-host-specific ELF loader path." >&2
  exit 1
fi
if patchelf --print-rpath "$OUTPUT_DIR/TerrariaUltrawidePatcher" | grep -q '/nix/store/'; then
  echo "The Nix-provided .NET SDK embeds Nix store paths. Use the official Microsoft .NET SDK to build a portable binary." >&2
  exit 1
fi
patchelf --set-interpreter /lib64/ld-linux-x86-64.so.2 \
  "$OUTPUT_DIR/TerrariaUltrawidePatcher"
printf 'Linux executable: %s\n' "$OUTPUT_DIR/TerrariaUltrawidePatcher"
