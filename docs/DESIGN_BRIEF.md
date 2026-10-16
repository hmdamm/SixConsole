# Design Brief — Six Console

## Subject & audience
A precision-control utility for one person: a PC player who keeps a Logitech-G-HUB recoil profile
running during matches and wants an independent app to hold that profile instead. The primary job of
the interface is: **pick operator → pick weapon → trust the state you're in, without reading prose.**
It must read like a control console, not a marketing page.

## Design system (after applying the named skills)

### Skills actually applied (and how)
- `anti-vibe-desktop-core` — banned palettes respected (no purple/indigo/neon, no gradients),
  12–13px type, 28–32px control heights, tabular numerals for all numbers, `user-select:none`
  semantics, locked viewport with inner scrolling, 1px hairline instead of shadow separation.
- `window-fluent-native` → read: `windows-fluent-native` — Segoe UI Variable Text typography (12–14px,
  Subtitle 16/600, Title 20/600), 4px control radius / 8px card radius, exact Fluent layer + text
  tokens for Light and Dark (Dark accent #60CDFF, Light accent #0078D4), left NavigationView with
  3px accent pill on the active item, SettingsCard-style 60px min rows.
- `pro-utility-monochrome` — 95% neutral palette, ONE restrained accent, split-pane workspace with
  1px hairlines, integrated 32px titlebar, compact 22px status bar, keyboard hint badges, native-size
  caption buttons (#E81123 close hover), no decorative shadows.
- `frontend-design` — pick a specific identity (a "control console" vernacular drawn from the
  subject: operator rosters, weapon cards, active state as an unmistakable instrument light), keep
  copy plain and single-purpose, no ALL-CAPS eyebrows, no mono-display tell, no boxed "AI card" grid.
- `ui-ux-pro-max` — queried its cache: `--design-system` run (dense dashboard, dark-first, JetBrains
  Mono dev-tool mood → adapted to Segoe mono only for figures), winui stack guidelines (NumberBox
  with Min/Max + SpinButtonPlacementMode for numeric settings, ContentDialog for confirmations,
  Expander for optional groups, merged dictionaries for tokens, multi-window rule), ux rules on
  keyboard/focus (visible focus ring of all interactive items, focus never obscured, wrong=_no_
  icon-only unlabeled controls).
- `impeccable` — **unavailable as content** (the skill file is empty; disclosed as such). Its intent
  (fixture-quality polish, no dead states) is folded into the audit checklist below.
- `audit` — **unavailable as content** (stub: "Not pinned."). Replaced by the built-in audit
  checklist in `AUDIT.md`, applied as a review gate over: contrast, keyboard-only operation, theme
  parity, every visible control wired to behavior, error/empty/disabled states, resize behavior.

### Token sheet (semantic)
Dark first (context: a tool most players run in dark rooms while gaming):
```
window.base          #202020   layer1.card         #2B2B2B   border.hairline #1D1D1D
control.fill         #2D2D2D   control.fill.hover  #323232   control.fill.active #272727
accent.action        #60CDFF   accent.alt          #0078D4
accent.success #6CCB5F   accent.warning #FFB900   accent.error #FF99A4
text.hierarchy       primary #FFFFFF, secondary #C5C5C5, muted #8A8A8A, disabled #787878
```
Light derivatives from `windows-fluent-native` (#F3F3F3 base, #FFFFFF cards, border #E5E5E5,
accent #0078D4, text #1A1A1A). System follows `Application.Current.FrameworkResources`' OS key
(`Application.RequestedTheme` = none = system).

Geometry: buttons/inputs/dropdowns 4px radius, cards/dialogs 8px, accent pill radius 99×3px.
Type: Segoe UI Variable Text for everything; no display font; numerals rendered equal-width in
weapon tables by using `Segoe UI Variable Display` and fixed-width cell columns.

### Information architecture
```
┌─ titlebar 32px ────────────────────────────────────────┐
│ ◉ Six Console      [Status pill: DISABLED/ACTIVE] ⛔STOP │ –  □  ✕ │
├─ nav (48px) ─┬─ content ───────────────────────────────┤
│  ▣ profile   │  Roster legend + search + ATK/DEF sets  │
│  ⚙ settings  │  ─ operator rows/cards in 2 groups      │
│  ? help      │  Weapon list (selected op)  │  Summary: │
│              │                             │ mode,     │
│              │                             │ pull,     │
│              │                             │ timing    │
├─ status bar 22px ──────────────────────────────────────┤
│ input backend OK ● │ caps OFF │ Ash (2/2) │ last event │
└────────────────────────────────────────────────────────┘
```
- **ProfileView** = single big card: enabled toggle, search, ATK/DEF sections (a segmented pair of
  grids like the Lua's 5-col roster, but selectable), the selected operator's weapon table with
  active row (accent pill rail instead of only color), and the summary panel that mirrors the Lua's
  status block (sens multiplier, rf key, current weapon's effective pull, timing range, mode chip).
- **SettingsView** = groups: Recoil & movement / Full-auto delay / Rapid-fire key & timing /
  Bindings / Theme. NumberBox on every numeric with live min≤max validation, inline error text.
- **HelpView** = backend health card, permissions note, troubleshooting per OS-level issues,
  emergency disable instructions.

### Motion
No page-load choreography, no per-card hover glow. Motion exists only where a state changes that
needs attention (enable toggle transitions its label; selected weapon row shows a 100ms pill slide).
Reduced-motion preference respected by skipping the pill slide animation.

## Copy rules
"Enabled/Disabled", not "Armed/Disarmed". "Emergency stop" not "Kill". Plain verbs, sentence case.
Every label states what the user gets or picks, never what the code calls it.
