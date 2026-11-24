# Nyx

**Ambient, audio-reactive screen-edge lighting and timed lyrics for Windows.**

Nyx turns whatever is playing on your PC into light around the edges of your display. It reads the current track from Windows media controls, extracts a palette from the album artwork, and renders an animated neon border that moves with the music. A fullscreen visualizer, a click-through desktop overlay, motion album artwork, and word-synced lyrics are built in.

An ambient Windows visualizer, redesigned and maintained by **Nyx**.

## Features

### Ambient edge lighting
- Multi-stop glowing border that hugs the display edges and corners.
- Album-art palette extraction with vibrance enhancement and a grayscale fallback.
- Rotating light, breathing animation, adjustable brightness, spread, and backdrop bloom.
- Audio-reactive pulse driven by WASAPI loopback capture.
- Optional beat-reactive Chromatic Split shader for lyrics, artwork/backdrop, or the full visualizer stage.
- Built-in color presets and a screen color dropper for per-track custom colors.
- Reactive glow that tints the artwork, cards, and player controls from the live beam color and pulse.

### Fullscreen visualizer
- Large artwork, blurred cover backdrop, and a dark ambient stage.
- Selectable blurred-artwork, audio-reactive mesh, or blended backdrop, with palette transitions and adjustable motion, reactivity, and vignette.
- Play/pause, previous/next, shuffle, repeat, seek bar, and master volume.
- Windowed or borderless fullscreen, with taskbar auto-reveal on hover.
- The track row keeps only Cover, Page, and Lyrics controls; dual-display split mode places album artwork on one monitor and a synced lyrics stage on another, with DPI-aware placement, hot-plug fallback, and display swapping.
- The secondary lyrics HUD provides playback, style, translation/language, romanization, and sync-offset controls, auto-hides when idle, and has independently configurable audio-reactive edge glow.
- Per-display settings popups independently adjust brightness, contrast, backdrop dimming, glow, and audio reactivity; secondary settings can be copied from the primary display or reset.
- Optional low-latency graphics mode reduces artwork blur resolution, video blur radius, and kinetic text effects while retaining the high-performance GPU preference.
- Fullscreen motion mode with animated covers, a blurred video backdrop, and an auto-hiding transport bar with seek, shuffle, repeat, and volume.
- Live glow controls with color presets, a screen dropper, brightness/spread sliders, and a reactive-glow toggle.
- Frosted-glass controls and a reactive glow pill that dims when paused.

### Desktop overlay
- Transparent, click-through edge glow that stays visible while you browse, work, or game.
- Taskbar-aware z-order handling.

### Timed lyrics
- Word- and syllable-level sync from TTML and QRC, with Enhanced LRC and plain LRC fallback.
- Two display styles: **Stage** (BitChord-inspired) and **Fisheye Lens** (Verci-inspired).
- Live lyric translation, romanization, manual sync offset, and a per-song provider picker.
- Instrumental and plain-text lyric support.
- Provider chain with disk caching and in-flight deduplication:
  LrcRed, BiniLyrics, BetterLyrics, PaxSenix, LyricsPlus, Unison, KuGou, LRCLIB,
  Musixmatch, and a Genius plain-text fallback.

### Motion artwork
- Animated covers from Apple Music editorial/album videos, Tidal 60fps video covers,
  the Vivi community canvas library, and Spotify Canvas.
- Optional Spotify `sp_dc` cookie unlocks Canvas streaming and can prioritize Spotify
  over Apple Music and Tidal; the other providers work without a cookie.
- Fuzzy title, album, and artist matching with background prefetch and a per-video
  on-disk cache, so loops start quickly and replay offline.
- Motion artwork can be disabled in Settings.

### Media and system integration
- Windows Global System Media Transport Controls (GSMTC): works with Spotify Desktop,
  Spotify Web, YouTube Music in Chrome/Edge, Apple Music, Tidal, VLC, and local players.
- Studio-quality artwork lookup from iTunes and YouTube Music, with palette re-extraction.
- System tray companion with fullscreen, overlay, settings, and exit actions.
- Single-instance handling, per-monitor DPI awareness, and optional high-performance GPU preference.
- Settings apply in real time and persist to disk.

## Requirements

- Windows 10 version 1809 (build 17763) or newer. Windows 11 recommended.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build and run from source.
- A media app that publishes to Windows media controls. No account, login, or API key is required.
- Optional: a PaxSenix API key for that provider's extended routes.
- Optional: a Spotify `sp_dc` cookie to unlock Spotify Canvas. Apple Music, Tidal, and
  community Canvas work without it.

## Run from source

```bash
git clone https://github.com/Anandpall/lmen-for-windows.git
cd lmen-for-windows
dotnet run --project LmenV2.csproj
```

You can also double-click `Run-LmenV2.bat`.

## Build a release

```bash
dotnet publish LmenV2.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The published app is written under `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\`.

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Space` | Play / pause |
| `Left` / `Right` | Previous / next track |
| `Up` / `Down` | Volume up / down |
| `L` | Open or close timed lyrics |
| `S` | Switch Stage / Fisheye lyrics style (while lyrics are open) |
| `M` / `F` | Toggle fullscreen motion artwork (when available) |
| `M` | Mute (when motion artwork is unavailable) |
| `F11` | Toggle windowed / fullscreen |
| `Esc` | Close the current layer; from the player, switch to the desktop overlay |

On the secondary lyrics display, `Esc` exits dual-display mode, `F11` toggles windowed/fullscreen, `S` changes lyric style, and `L` shows or hides lyrics.

Double-click the album art to enter fullscreen motion mode.

## Settings and local data

| Path | Contents |
|---|---|
| `%APPDATA%\LmenV2\settings.json` | Glow, audio, lyrics, motion artwork, Chromatic Split, and dual-display settings |
| `%LOCALAPPDATA%\LmenV2\Lyrics` | Cached lyric provider responses |
| `%LOCALAPPDATA%\LmenV2\LyricsTranslation` | Cached translations and romanizations |
| `%LOCALAPPDATA%\Lmen\CanvasCache` | Downloaded motion artwork |

Settings include primary and secondary display brightness, contrast, and backdrop dimming; display-specific glow brightness, spread, and audio reactivity; rotation speed, breathing amount, lyric beat effects, translation language, lyric offset, motion artwork on/off, Spotify Canvas priority, an optional Spotify `sp_dc` cookie, and an optional PaxSenix API key.

## Project layout

| Path | Purpose |
|---|---|
| `App.xaml.cs` | Startup, single-instance handling, display-change handling, and window/tray lifecycle |
| `Rendering/` | Edge beam geometry, glow profiles, backdrop blur |
| `Services/` | Media sessions, audio capture, color extraction, artwork, lyrics, settings |
| `Views/` | Fullscreen visualizer, secondary lyrics stage, desktop overlay, and settings windows |
| `Tray/` | System tray icon and menu |
| `Models/` | Track metadata and lyric timing models |
| `Helpers/` | Win32 interop, monitor enumeration, and per-monitor DPI placement |

## Tech stack

- .NET 8, WPF, and WinForms (tray icon)
- NAudio.Wasapi for loopback audio capture
- Windows.Media.Control for media session detection
- Custom color quantization and palette extraction
- TTML, QRC, Enhanced LRC, and LRC lyric parsers
- Win32 interop for click-through overlays and taskbar handling

## Credits

- Designed and maintained by **Nyx**.
