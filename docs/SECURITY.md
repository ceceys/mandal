# Security notes

Mandal is a local desktop tool. It has **no network code**: no update checks, no telemetry, no uploads. Everything it produces stays in the capture folder (`%APPDATA%\Mandal\Clips` by default) and `%APPDATA%\Mandal` (settings, item shortcuts, log).

## Inputs the app does not trust

| Input | Protection |
|---|---|
| Clipboard data | Only image formats are read. "PNG" payloads must start with the PNG signature and be ≤ 64 MB; bitmaps above 100 MP are ignored. File-drop lists are only compared against the capture folder, never opened. Our own copies are suppressed for 2 s and deduplicated by SHA-256. |
| Files in the capture folder | Only `*.png` (with signature check) and `*.txt` (≤ 1 MB) are loaded, each in its own try/catch. A corrupt file is logged and skipped. |
| `settings.json` | Malformed JSON falls back to defaults. Numeric values are clamped, shortcut strings are length-limited and parsed strictly. An unusable capture folder falls back to the default with a notification. |
| `item-hotkeys.json` | Keys must be relative paths inside the capture folder (no absolute paths, no `..`); gestures must parse. Entries whose files are gone are pruned. |
| Shortcut strings | Parsed into modifier flags + a virtual-key code; nothing is executed or evaluated. |

## Process launches

The app starts only: the default viewer for a `.png`/`.txt` that is verified to be inside the capture folder, `explorer.exe` for the capture folder, and the settings file. Windows file names cannot contain `"`, so the quoted arguments cannot be broken out of.

## Single instance

A `Local\` (per-session) mutex and event are used. The only effect another process can trigger through the event is "show the line".

## Reporting

Open an issue on GitHub. Please do not include screenshots that contain personal data.
