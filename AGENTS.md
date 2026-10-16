# AGENTS.md — Six Console (r6\)

Durable product/spec memory: read `Implementation_Plan (1).md` at the start of every session — it is
the authoritative spec. Supporting docs: `docs/ARCHITECTURE.md`, `docs/DESIGN_BRIEF.md`,
`docs/SOURCE_CHECKLIST.md`.

Hard rules:
- `message (2).txt` (the Lua source) is canonical; never edit it, and keep
  `SixConsole.Tests/SourceFixtures/lua-source.txt` byte-identical to it.
- Windows-only target. WinUI 3 + Windows App SDK, C#/.NET, x64.
- Data integrity is checked by tests that regex-parse the Lua fixture and compare against the
  catalogue; never "re-balance" profile values.
- Safety: cancellation everywhere; synthetic input distinguishable from physical; no anti-cheat
  evasion, no injection, no telemetry.
- Design tokens only from `windows-fluent-native` / `pro-utility-monochrome` (no violet/indigo,
  no gradients, no emoji-as-icons, 12–13px type, 28–32px controls).
