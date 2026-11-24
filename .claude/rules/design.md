---
trigger: model_decision
description: UI design rules (Wintor-style Windows 11 look) — use when changing any form, control, colour, icon or layout
---

# Design (Wintor style on WinForms)
- Colours only via `Theme.*`, never literals: `Chrome` (title bar, nav), `Bg` (page layer), `Card`; text `Text`/`Muted`/`Faded`;
  borders `CardStroke`/`CtlStroke`; hover/selection `Line`; `Accent` = Windows accent colour; progress `Blue`/`Green`.
- Palette = Tailwind zinc. Dark: Chrome #09090B, Bg #18181B, Card #27272A. Light: #F4F4F5, #FAFAFA, white.
- Sizes only via `Theme.Dp(int)`; fonts via `Theme.UI(size, style)` (Segoe UI Variable; Bold → Semibold).
- Radius: cards 8, controls 4. Rows (tables, nav) 40 dp; fields and drop-downs 32 dp high, 200 dp wide.
- Selected item: soft fill + short accent pill on the left (3×16 dp).
- Icons: code passes old Segoe Fluent code points; `Icons.Map` maps them to Hugeicons names. New icon: add the name to
  `tools/icons.mjs`, run `npm pack @hugeicons/core-free-icons` + `node tools/icons.mjs package/dist/cjs`, add to `Icons.Map`.
- Buttons only via `Theme.Button(text, primary, glyph, trailing)`; icon-only buttons are automatically `Subtle`.
- Choice → `Dropdown`; settings switch → `OnOff` (label On/Off); number → `Stepper` or `NumberBox`.
- Never change `Text` or other properties inside `OnPaint` (endless repaint). Use a flag such as `DrawContent`.
