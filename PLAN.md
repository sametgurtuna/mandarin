# Mandarin — Project Plan

Windows-native equivalent of "Tangerine for Mac" (tangerineformac.com): a zero-click,
fully offline file converter. Drag a file onto a floating panel, hold Shift to convert,
hold Shift+Alt for advanced tools (compress, crop, trim, split, merge, strip metadata).

Stack: .NET 8, WPF, MVVM (CommunityToolkit.Mvvm). Conversion engines: FFmpeg (video/audio),
ImageMagick via Magick.NET (images), a PDF library (render/export), SharpCompress (archives).
100% offline — no telemetry, no network calls.

Work through the phases below **in order**. At the end of each phase: the app must build
and run, the phase's features must be demonstrably working, and you must give a short
summary of what changed and exactly what I should click/test to verify it. Do not start
the next phase until I confirm the current one works.

---

## Phase 0 — Skeleton
- Solution structure: `Mandarin.App` (WPF UI), `Mandarin.Core` (conversion logic, no WPF
  references), `Mandarin.Shell` (placeholder for Explorer context menu, Phase 6),
  `tests/Mandarin.Core.Tests`.
- System tray icon with a right-click menu (Open, Settings, Quit).
- A small always-on-top, draggable, borderless floating panel (the "bubble"). Position is
  saved to user settings and restored on next launch.
- Basic orange/mandarin-themed icon and color palette (an original design, not a copy of
  Tangerine's actual logo).
- Settings persistence (JSON file in `%AppData%\Mandarin`): panel position, last-used
  formats per file type, language.

## Phase 1 — Image conversion MVP
- Drag-and-drop a file onto the panel.
- Drop with no modifier → format menu appears, filtered to formats relevant to the
  dropped file's type. Keyboard-navigable (arrows, Enter, Esc), same as Tangerine.
- Drop while holding **Shift** → instantly applies the most likely/most recently used
  conversion, no menu.
- Image conversion via Magick.NET: JPG, PNG, WebP, HEIC, TIFF, SVG, AVIF, BMP, plus
  export to PDF and DOCX.
- Progress indicator + completion toast notification with "Open folder" shortcut.
- This phase should be fully working end-to-end before moving on.

## Phase 2 — Video & audio
- FFmpeg integration (shell out to `ffmpeg.exe`, bundled as a local binary — no download
  at runtime).
- Video formats: MP4, MOV, MKV, WebM, AVI, WMV, GIF. Audio formats: MP3, M4A, WAV, FLAC,
  OGG, Opus, AIFF, WMA. Video → MP3 audio extraction.
- Progress reporting for long-running conversions (FFmpeg progress parsing).

## Phase 3 — PDF, text, subtitles
- PDF → DOCX, JPG, PNG, TXT (JPG/PNG at 300 DPI, all pages).
- TXT → PDF, JPG, PNG, SRT, VTT (UTF-8).
- SRT / VTT / TXT conversions (simple custom parser, no heavy dependency needed).

## Phase 4 — Archives
- Create/extract ZIP, TAR, GZIP via SharpCompress.
- RAR: extract only (no creation — licensing).

## Phase 5 — Advanced tools (Shift+Alt menu)
- Compress (image/video/audio/PDF, with a quality/target-size option). Target size was
  added later, in the post-roadmap UI/UX pass, along with per-format options (image max
  long edge + lossless, video resolution cap, audio bitrate). PDFs offer quality only:
  a target-size search would mean re-rendering every page per probe.
- Crop (small popup window with a drag-to-select rectangle) for images and video.
- Trim (start/end time picker) for video/audio.
- Split (PDF by page range; video/audio at a chosen timestamp, producing two files).
- Strip metadata (EXIF and other embedded metadata, for privacy).
- Merge moved to Phase 6 — it needs multi-file input, which doesn't exist until batch
  drop is built there. (Decided when implementing Phase 5.)

## Phase 6 — Polish (done)
- Batch conversion: drop multiple files at once; converts all to one common target
  format (menu), or each to its own best-guess format (Shift-drop). Done.
- Merge (multiple PDFs into one; multiple video/audio clips into one) — carried over from
  Phase 5. Triggered by Shift+Alt-dropping 2+ files of the same supported type. Done.
- Explorer right-click integration via `Mandarin.Shell`. Scope changed from the original
  plan: no MSIX sparse package / IExplorerCommand submenu (that needs MSIX, which was
  declined — see below). Instead, a classic per-user registry verb adds a single static
  "Convert with Mandarin" entry (no per-format submenu) that launches the app with the
  clicked file; a named-pipe single-instance handoff means it reuses an already-running
  instance instead of opening a second tray icon. Done, with this reduced scope.
- MSIX packaging: declined — no code-signing certificate available, and MSIX without one
  can't be installed outside this machine. Distribution is a self-contained single-file
  `.exe` via `dotnet publish` instead (see `CLAUDE.md` Commands/Packaging). Revisit MSIX
  if a real signing story (purchased cert or Store listing) shows up later.
- Manual "Check for updates" button only — no background/auto network calls. Done, but
  as an honest stub: there's no release channel yet for it to actually check against.
- Localization scaffolding (English + Turkish) via `Strings.resx`/`Strings.tr.resx`.
  Done for static UI text (dialogs, tray menu, toast titles); dynamic error messages
  generated by `Mandarin.Core` remain English-only (see `CLAUDE.md` Stack section for
  why) — a known gap, not a blocker.

---

## Post-roadmap work (after Phase 6)

A UI/UX pass, done in this order, each verified before the next:
1. Design system: `Themes/Theme.xaml` tokens + templated controls; every dialog rebuilt on
   one card pattern (header strip, footer action bar, Esc to dismiss).
2. Windows 11 acrylic backdrop on every dialog, the toast and the progress HUD. The radial
   wheel keeps painted glass: it is circular, and Windows can only back a rectangular,
   non-layered window (a layered window also ignores `SetWindowRgn`, so a blur cannot be
   clipped to a circle).
3. Light/dark themes, following the Windows app mode by default.
4. FFmpeg first-run flow: locate ffmpeg.exe, remembered in settings.
5. Output-name preview in every tool dialog.
6. Crop: handles, aspect presets, arrow-key nudging, thirds guides, dimmed surround.
7. Trim: real filmstrip frames sampled from the video.
8. Compress: quality or target size, plus per-format options.

Still open from that pass: a batch queue with per-file status, and one-click presets
("Web JPG 1600px q80", "Discord MP4 under 10MB") as a second ring on the wheel.

## Non-negotiable constraints (apply to every phase)
- `Mandarin.Core` must have zero WPF/UI dependencies — conversion logic is reusable from
  a CLI or a different UI later.
- New formats are added via a single `IConverter` implementation (strategy pattern), not
  by growing an if/else chain.
- No network calls anywhere in the app (no analytics, no telemetry, no silent
  update-checks).
- Errors (corrupt file, missing codec, unsupported conversion) must show a clear message
  and never crash the app.
- Unit tests for format detection and the core conversion flow in `Mandarin.Core.Tests`.
