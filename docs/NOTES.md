# NOTES — log
Format: `YYYY-MM-DD — symptom → cause → fix → commit`

- 2026-10 — Quran texts broken in CI builds → checkout converts to CRLF → split lines by `\n`, trim `\r`; selftest checks it → 84da17d
- 2026-10 — tajweed colours drift off their letters → measuring and drawing used different `TextRenderingHint` → use AntiAliasGridFit for both
- 2026-11-10 — settings page hangs on open → `Dropdown.OnPaint` set `Text` (triggers repaint forever) → `DrawContent` flag instead → c0ccc09
- 2026-11-10 — wrong surah numbers in search → `QuranText.Sura.N` is the ayah COUNT, not the number → number = index + 1 → c0ccc09
- 2026-11-10 — main window minimized by itself during tests → owner's real clicks hit the on-screen test window; title bar acted on mouse-up without mouse-down → buttons act only on press+release on the same button; test windows off-screen → c0ccc09
- 2026-11-10 — SVG `\u` escapes in Python edit scripts became literal private-use chars → the write tool unescapes them → build `\u` with `chr(92) + 'u'`

## Known limitations
- Windows (Git Bash, no Developer Mode symlinks): `ln -s` makes COPIES, so `.claude/skills` and `.claude/rules` are real folders
  copied from `.agents/skills` / `.agents/rules`, not symlinks. After changing anything in `.agents/skills` or `.agents/rules`,
  re-copy: `cp -a .agents/skills/. .claude/skills/ && cp -a .agents/rules/. .claude/rules/`, check with `diff -r`.
- Git Bash: jq is installed by winget but not on PATH — use
  `export PATH="$PATH:$(cygpath "$LOCALAPPDATA")/Microsoft/WinGet/Packages/jqlang.jq_Microsoft.Winget.Source_8wekyb3d8bbwe"`.
- `python3` is the Microsoft Store stub; use `py`.
- IMPORTANT: `.claude/hooks/protect-secrets.sh` needs `jq`. Without jq on PATH it exits 0 and blocks NOTHING (fails open).
  With jq on PATH it blocks `.env` (exit 2) — verified 2026-11-24. Fix: put jq on the user PATH (see above).
