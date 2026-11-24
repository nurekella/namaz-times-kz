# DECISIONS
Format: `YYYY-MM-DD — decision — why — alternatives`

- 2026-10 — Muftiyat is named ҚМДБ in Kazakh, ДУМК in Russian, SAMK in English — owner's request — one name everywhere (commit 3339df9)
- 2026-10 — Al-Muntakhab is the default Russian Quran translation; Abu Adel and Kuliev selectable — Kuliev's is widely criticised — Alyautdinov (needs permission, not obtained) (commit 7e4fe8f)
- 2026-10 — tajweed colouring from cpfair/quran-tajweed (CC BY 4.0) over the 2017 Tanzil Uthmani text — offsets match that exact text — a tajweed font (commit ca05ced)
- 2026-11 — one main window with a navigation pane instead of separate windows — owner's request — separate windows (commit 479550c)
- 2026-11-10 — Wintor-style design: own title bar with search, Tailwind zinc palette, Windows accent, Hugeicons (MIT) — owner asked for "exactly Wintor's design" — plain Fluent 2 (commit c0ccc09)
- 2026-11-13 — no Microsoft Store publication for now — owner declined; would need an MSIX build without self-update — MSI submission (needs a code-signing certificate)
- 2026-11-11 — fluent-ui-design plugin not used — it targets React/web Fluent UI, not WinForms — install via `/plugin` if ever needed
