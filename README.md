# Nyx

**Ambient, audio-reactive screen-edge lighting and timed lyrics for Windows.**

Nyx turns what plays on your PC into a reactive neon border, featuring a fullscreen visualizer, click-through desktop overlay, motion artwork, and word-synced lyrics, maintained by **Nyx**.

## Core Features

- **Ambient Edge Lighting:** Multi-stop glowing borders, album-art palette extraction, WASAPI loopback audio reactivity, chromatic split shaders, and custom color presets/droppers.
- **Fullscreen Visualizer:** Large artwork, blurred/mesh backdrops, full transport controls, dual-display lyrics splitting, low-latency modes, and motion artwork playback.
- **Desktop Overlay:** Transparent, click-through edge glow with taskbar-aware z-order handling.
- **Timed Lyrics:** Word- and syllable-level sync (TTML, QRC, Enhanced/Plain LRC), Stage and Fisheye display styles, and multi-provider lookup chains (including Musixmatch, Genius, and LRCLIB).
- **Motion Artwork:** Animated loops from Apple Music, Tidal, Spotify Canvas, and Vivi, with local caching.
- **Media & System Integration:** Compatible with GSMTC media sources (Spotify, YouTube Music, Apple Music, Tidal, VLC), iTunes/YouTube Music metadata lookup, a system tray companion, and real-time settings persistence.

## System Requirements

- Windows 10 (version 1809 / build 17763 or newer; Windows 11 recommended).
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (required for building from source).
- Active media app supporting Windows media controls (no account or API key required by default; optional PaxSenix or Spotify `sp_dc` credentials can be provided).

## Quick Start & Building

To run from source:
```bash
git clone https://github.com
cd lynny
dotnet run --project LmenV2.csproj
```
*(Alternatively, run `Run-LmenV2.bat`).*

To build a standalone release:
```bash
dotnet publish LmenV2.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
*(Output location: `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\`)*

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Space` | Play / pause |
| `Left` / `Right` | Previous / next track |
| `Up` / `Down` | Volume up / down |
| `L` | Open or close timed lyrics |
| `S` | Switch Stage / Fisheye lyrics style |
| `M` / `F` | Toggle fullscreen motion artwork (or mute if unavailable) |
| `F11` | Toggle windowed / fullscreen |
| `Esc` | Close current layer or switch to desktop overlay |

*(Double-click album art for fullscreen motion mode; secondary display controls mirror these actions).*

## Settings, Layout, and Tech Stack

Configuration and cache paths reside under `%APPDATA%\LmenV2\settings.json` and local cache directories, powered by .NET 8, WPF, NAudio.Wasapi, and Win32 interop. See the full codebase structure and detailed settings breakdown in the project files.

- Designed and maintained by **Nyx**.
