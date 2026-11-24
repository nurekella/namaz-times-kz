---
name: visual-check
description: Screenshot the widget and main-window pages in dark and light themes to check UI changes. Use when forms, controls, colours, icons or layout change.
---

# Visual check
1. Back up the owner's settings: `cp "$APPDATA/NamazTimes/settings.json" <scratch>/settings.bak`.
2. Harness = a separate console project (outside the repo) referencing a COPY of `bin/Release/net10.0-windows/NamazTimes.dll`
   (rebuild the harness after copying — the reference is copied locally). It:
   - loads settings, sets `ThemeMode` (1 dark, 2 light), `Lang`, `TopMost = false`;
   - creates `new Widget(s)`, calls private `Widget.Page(key, null)` and reads private field `main` via reflection;
   - places the main window OFF-SCREEN (`Location = (-2300, 10)`) so it never catches the owner's clicks;
   - pumps `Application.DoEvents()` ~0.5 s per page, snaps with `PrintWindow(hwnd, dc, 2)`.
3. Pages to cover: today, settings, month, quran, tasbih, holidays, zakat; plus title-bar search (find the `SearchBox`
   whose parent is `TitleBar`).
4. Run with a timeout (e.g. `timeout 200`). A hang usually means an endless repaint or layout loop — see docs/NOTES.md.
5. Restore settings: `cp <scratch>/settings.bak "$APPDATA/NamazTimes/settings.json"`.
6. Look at every image; compare with `.agents/rules/design.md`. Send key screenshots to the owner.
