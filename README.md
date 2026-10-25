# ShaojiaHelper

A dumb code mod for game Celeste. You can download the mod at gamebanana [here](https://gamebanana.com/mods/684784).

## Building from source

Requires the .NET 8 SDK (or newer) and `rsync`, plus a local Celeste + Everest installation.
On Ubuntu/Debian: `sudo apt install dotnet-sdk-8.0 rsync`.

1. Tell the build where Celeste lives. Create a `.celeste-path` file in the repo root
   containing the absolute path to your Celeste folder (one line, no quotes):

   ```sh
   printf '%s\n' "$HOME/.steam/steam/steamapps/common/Celeste" > .celeste-path
   ```

   On Steam for Linux the folder is usually `~/.steam/steam/steamapps/common/Celeste`
   (use `realpath` on it if unsure). The file is git-ignored.

2. Build and deploy:

   ```sh
   ./sync_and_build.sh
   ```

   The script mirrors this repo into `<Celeste>/Mods/ShaojiaHelper` (skipping `Source/`,
   `Code/`, `.git/`, ...), then runs `dotnet build Source -c Release`. The project copies
   the resulting `ShaojiaHelper.dll` and `.pdb` into `Mods/ShaojiaHelper/Code`.
   Restart Celeste afterwards so Everest picks up the new build.

In VS Code, open this folder and press `Ctrl+Shift+B` to run the same task
(see `.vscode/tasks.json`).

## Issue Reporting

If you find bugs, create a new issue or a pull request to let me know.

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
