# Changelog

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
