<div align="center">

# ProxyNodeHub

### GitHub free-node repository monitor & aggregator

**English** | [中文](README.zh-CN.md)

![ProxyNodeHub main interface](images/main.png)

</div>

---

> ⭐ **If ProxyNodeHub is useful to you, please consider leaving a Star — it is the biggest motivation I have to keep this project going.**

---

## Overview

ProxyNodeHub finds active free-node repositories on GitHub: it profiles their activity, de-duplicates the nodes they publish, and hands you a ready-to-use subscription link. One click on **Search & analyse**, and the rest of the triage is done for you.

No more opening repository after repository, comparing by hand, stitching a subscription together yourself. ProxyNodeHub surfaces the repositories that are maintained hardest and updated most often, then merges and de-duplicates their nodes into a single export. It also digs out the quiet, easy-to-miss repositories — in this domain, low traffic usually means low load and a longer-lived node. The overlooked repository is often the good one.

## Features

| | |
|---|---|
| **Six-layer detection engine** | Feature library → hardcoded mapping → file-tree scan → common-path probe → README parse → fallback candidates |
| **Concurrent search** | 8 keyword queries issued in parallel across GitHub's REST API |
| **Activity scoring** | 100-point model over commit frequency, regularity, automation and obscurity |
| **Node de-duplication** | Keyed on `server:port`, merged into one subscription export |
| **Four processing modes** | 🚀 Turbo / ⚖️ Standard / 🔍 Deep / 🐢 Compatible, with per-mode concurrency |
| **Auto proxy** | 13 accel mirrors, automatic latency test, fail-over and 5-minute result caching |
| **Search-history filter** | Recently searched repositories are skipped automatically (1–30 days, adjustable) |
| **Favourites** | Separate view, persisted across restarts |
| **Feature library** | Learns the paths that worked, so the next run is faster |
| **Persistent logs** | Rolling log with configurable retention and one-click purge |

## Requirements

- **Self-contained build** — Windows 10 or later (x64). Nothing else to install.
- **Framework-dependent build** — Windows 10 or later (x64) plus [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

## Getting started

1. Grab a build from [Releases](../../releases) — `ProxyNodeHub_v0.0.1_self-contained.zip` if you want zero prerequisites.
2. Unzip anywhere and run `ProxyNodeHub.exe`.
3. Press **F5** (or click 搜索并分析) to search and analyse.

A GitHub token is optional but recommended: the anonymous search API allows 60 requests per hour, a token raises that to 5,000. Paste it into the key field and it is stored in the Windows credential store, encrypted per-user.

## Building

```bash
# self-contained single file (~68 MB, includes the runtime)
dotnet publish src/ProxyNodeHub.csproj -c Release -r win-x64 -p:SelfContained=true -o dist/self-contained

# framework-dependent (~3 MB, needs the .NET 8 runtime on the machine)
dotnet publish src/ProxyNodeHub.csproj -c Release -r win-x64 -p:SelfContained=false -o dist/framework-dependent
```

Requires the .NET 8 SDK with the Windows Desktop workload.

> **Note on size:** the self-contained build cannot be trimmed. `PublishTrimmed` is unsupported for WinForms (NETSDK1175), so ~65 MB is the floor for a .NET 8 WinForms single-file package.

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `F5` | Start / cancel search |
| `Ctrl+C` | Copy the best subscription link |
| `Ctrl+L` | Toggle the detail panel |
| `Ctrl+D` | Toggle favourites |
| `Ctrl+F` | Focus the filter box |
| `Esc` | Close dialogs |

## Configuration

Everything the tool learns lives outside the binary, under `%AppData%\ProxyNodeHub\`:

| File / path | Purpose |
|---|---|
| `known_repos.json` | Bundled next to the EXE — known repository → subscription-path mappings |
| `searched_repos.json` | Search history, with a per-entry expiry |
| `log.txt` | Rolling log, trimmed to the retention window on launch |
| feature library JSON | Successful probe paths, reused by layer L1 |

## Project structure

```
src/
├── Program.cs               # entry point
├── MainForm.cs              # main window, toolbar, grid, log panel
├── SubscriptionFinder.cs    # the six-layer detection engine
├── GitHubService.cs         # GitHub HTTP client + proxy mirrors + latency test
├── FeatureLibrary.cs        # feature library + repository classifier
├── CustomFeatureLibrary.cs  # user-defined signatures
├── NodeParser.cs            # node parse / de-duplicate / merge
├── KnownRepoLoader.cs       # known_repos.json loader
├── SearchHistory.cs         # search-history persistence
├── LogStore.cs              # log persistence + retention
├── Settings.cs              # settings, cache, favourites
├── Models.cs                # data model
├── Theme.cs                 # colour tokens
├── Fonts.cs                 # font system
├── ModernButton.cs          # button control
├── BufferedGridView.cs      # double-buffered grid
├── Win32Interop.cs          # Win32 interop
├── LanguageFilter.cs        # language filter
├── ExportDialog.cs          # export dialog
├── FeatureLibraryDialog.cs  # feature-library manager
├── AboutDialog.cs           # about dialog
├── ProxyNodeHub.csproj
├── known_repos.json         # known-repository mappings
└── logo.ico / logo.png      # application icon
```

## Tech stack

- C# / WinForms on .NET 8 (`net8.0-windows`, `win-x64`)
- Windows 10 or later, x64
- No third-party NuGet packages beyond `System.Security.Cryptography.ProtectedData`

## Author

**Zoyaya** — [github.com/wanvfx](https://github.com/wanvfx)

## License

Released under the [MIT License](LICENSE).
