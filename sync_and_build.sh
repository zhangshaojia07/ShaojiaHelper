#!/usr/bin/env bash
# Sync ShaojiaHelper assets into Celeste/Mods/ShaojiaHelper and build (Linux version).
set -euo pipefail

REPO_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_FILE="$REPO_DIR/.celeste-path"

die() { printf '[error] %s\n' "$*" >&2; exit 1; }

[[ -f "$CONFIG_FILE" ]] || die "Missing $CONFIG_FILE. Create it in the repository root and put the Celeste install directory in it, e.g.:
  printf '%s\n' \"\$HOME/.steam/steam/steamapps/common/Celeste\" > \"$CONFIG_FILE\""

# Read the first line, stripping BOM, CR, surrounding whitespace and stray quotes, and expanding ~
CELESTE_DIR="$(head -n 1 "$CONFIG_FILE" \
    | sed -e '1s/^\xEF\xBB\xBF//' -e 's/\r$//' -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//')"
CELESTE_DIR="${CELESTE_DIR%\"}"
CELESTE_DIR="${CELESTE_DIR#\"}"
CELESTE_DIR="${CELESTE_DIR/#\~/$HOME}"

[[ -n "$CELESTE_DIR" ]] || die "$CONFIG_FILE is empty"
[[ "$CELESTE_DIR" == /* ]] || die ".celeste-path must contain an absolute path, got: $CELESTE_DIR"
[[ -f "$CELESTE_DIR/Celeste.dll" ]] || die "No Celeste.dll in '$CELESTE_DIR'; please check .celeste-path"
[[ -d "$CELESTE_DIR/Mods" ]] || die "'$CELESTE_DIR/Mods' does not exist; launch Celeste / Everest once first"

command -v dotnet >/dev/null 2>&1 || die "dotnet not found. Install the .NET SDK first: sudo apt install dotnet-sdk-10.0"
command -v rsync >/dev/null 2>&1 || die "rsync not found: sudo apt install rsync"

MOD_DIR="$CELESTE_DIR/Mods/ShaojiaHelper"
mkdir -p "$MOD_DIR"

# Same semantics as robocopy /E /PURGE with /XD and /XF in the old Windows script
printf '[1/2] Syncing assets -> %s\n' "$MOD_DIR"
rsync -a --delete \
    --exclude 'Source/' \
    --exclude 'Code/' \
    --exclude 'workplace/' \
    --exclude '.git/' \
    --exclude '.vscode/' \
    --exclude '.agents/' \
    --exclude '.aws/' \
    --exclude '.codex/' \
    --exclude 'sync_and_build.sh' \
    --exclude 'README.md' \
    --exclude '.gitignore' \
    --exclude '.celeste-path' \
    "$REPO_DIR/" "$MOD_DIR/"

printf '[2/2] Building (Release)\n'
dotnet build "$REPO_DIR/Source" -c Release -p:CelesteRoot="$CELESTE_DIR"

printf 'Done: %s\n' "$MOD_DIR/Code/ShaojiaHelper.dll"
