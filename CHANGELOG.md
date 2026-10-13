# Changelog

## 1.5.0 — 2026-10-13

- Nothing is ever deleted from disk. ✕ (Remove from line), "Clear line" and the optional auto-cleanup only take cards off the line; the PNG/TXT stays in the capture folder. Removed cards are listed in `hidden.json` (relative paths, grouped per action) so they stay off the line after a restart and are not even decoded at startup. Tray menu → "Put back last removed" re-hangs the last group in time order. Entries whose file was deleted by hand are dropped at startup.
- Right-hand card: "Delete all" became "Clear line" (no confirmation needed since nothing is lost; pinned cards and cards with a shortcut stay) and a new "Clear clipboard" button empties the Windows clipboard, briefly showing "Cleared ✓". The card grows with the longest label, so long translations are no longer cut off.
- Fixed: the update question could be cut off on some PCs (fixed 300 px window; long release notes pushed Yes/No out of view, worse with display scaling). The confirmation card now sizes to its content, keeps long text in its own scrolling area and is placed inside the screen's work area, so the buttons are always visible.
- The update question shows the new version's notes in the app's language, read from `Resources/Notes.<lang>.md` at the release tag (falls back to English, then to the release description as plain text instead of raw Markdown).
- Release builds publish a one-line description, so versions up to 1.4.0 (which print the description in a fixed-height window) can show their Yes button too.

## 1.4.0 — 2026-10-12

- Pin cards: hover a card and click the pushpin at the bottom right, or right-click → Pin. Pinned captures and notes stay at the start of the line with a blue clothespin and keep their order across restarts (`pins.json`). Drag a pinned card sideways to reorder it; pull it below the line to drag it into another app instead; `Esc` cancels. "Delete all" and auto-delete leave pinned cards alone.
- Start with Windows is on by default (tray menu or Settings → Behavior). The choice is stored in `settings.json`, and at every start the startup entry is pointed at the running exe, so moving or updating the portable exe no longer breaks it. With the installer, its "start automatically" choice is kept; uninstalling removes the entry.
- A shortcut can be the numpad `+` on its own (e.g. `"HotkeyRegionAlt": "+"`) or with modifiers (`Ctrl++`); the shortcut box now accepts it. Previously `+` was reported as an invalid shortcut.

## 1.3.1 — 2026-10-09

- Fixed: changing shortcuts in Settings. Global hotkeys are suspended while a shortcut box has keyboard focus, so pressing e.g. Ctrl+Shift+F fills the box instead of triggering a capture (same for per-card shortcut assignment).
- Invalid shortcut strings in `settings.json` are ignored instead of being reported at every start.
- The corner clothespin is recreated if its window is closed unexpectedly; showing or hiding the line no longer throws in that case.
- The corner clothespin can be toggled from the tray menu; right-click the clothespin → Hide.

## 1.3.0 — 2026-10-09

- Drag and drop: the clothespin snaps off, a small copy of the card follows the cursor, the line pulls up while dragging and comes back if the drop is cancelled.
- "Delete all" sits on its own row below a divider; About box in Settings with version, GitHub/LinkedIn, update check and release notes. Settings content scrolls on small screens.
- Automatic updates from GitHub Releases (on by default, Settings → Behavior; "Check for updates" in the tray menu). The app checks 20 s after start and once a day, downloads the new installer or portable exe, verifies it against `SHA256SUMS.txt` from the release, and asks before installing; the installer runs silently and the app restarts. This is the app's only network access.

## 1.2.0 — 2026-10-09

- Preview window: double-click a card (or the magnifier button / right-click → Preview) to open it large at the top of the screen. Resizable from the edges, movable by its title bar, mouse-wheel zoom, drag to pan, `0` fit / `1` actual size. Optionally stays above other apps (pin button, also in Settings). Size and position are remembered.
- Corner tab is now just a diagonal clothespin on a transparent background; it pops forward and swings when the mouse approaches.
- The line puts itself away as soon as the mouse leaves it (default on, Settings → Behavior).
- "Delete all" button on the right-hand card with an in-theme confirmation card; optional confirmation for single deletes (Settings).
- Flat transparent clothespin as the app icon.
- Notes: "Add note" on the right-hand card opens a note card; the note hangs on the line as a text card. Right-click → Edit (or the pencil in the preview) to change it. A cancelled note is kept as a draft and comes back next time.
- Select text directly on a note card on the line and release to copy just that part (double-click selects a word); drag the clothespin to move the card. Default on, Settings → Behavior. The same works in the preview, plus "Copy all".
- Context menu restyled as a paper card.
- Storage: "Keep captures and notes forever" is on by default; nothing is ever deleted automatically. Optional auto-delete after N days can be enabled in Settings (cards with a shortcut are always kept).
- The corner clothespin and hot corner can be placed in any screen corner (Settings).
- Settings has an "Apply" button so several changes can be checked without closing the window.
- Quick in-place editing of notes: hover a note card and click the pencil, type, Ctrl+Enter (or click elsewhere) saves, Esc cancels.

## 1.1.0 — 2026-10-09

- 12 languages (tr, en, de, fr, es, it, pt, ru, ar, zh, ja, ko); follows the system language, switchable in Settings.
- Transparent line: cards hang directly over the desktop.
- Fixed control card at the right end: Capture, Settings, Put away.
- Settings window with editable shortcuts, behavior options and capture folder.
- Stretching line: cards shrink as more items are pinned (tiers at 12/16/24/32/40/50/60), overflow scrolls with arrows or the mouse wheel.
- Per-item persistent shortcuts (e.g. Ctrl+Alt+1 copies that item, even after a restart).
- Convert an image to text with one click (Windows OCR); text cards can be copied and dragged like images.
- Captures stored in day folders (`Clips\yyyy-MM-dd\HH-mm-ss.png|.txt`).
- Top-left hot corner and a small corner tab open the line.
- New logo, Inno Setup installer, GitHub Actions build.
- Hardening: PNG signature and size checks for clipboard data, settings range validation, folder fallback, restricted process launches.

## 1.0.0 — 2026-10-09

- First version: region / full-screen / window capture, clipboard watching, click-to-copy, drag and drop, delete, persistent PNG storage, tray menu.
