# Source-to-Feature Checklist

Canonical source: `message (2).txt` (Lua). Reference fixture at
`SixConsole.Tests/SourceFixtures/lua-source.txt` (byte-identical copy of the original).

## Globals from the source (must match exactly)
| Source | Default |
|---|---|
| isEnabled | false |
| currentOpIndex | 1 |
| currentGunIndex | 1 |
| recoilMultiplier | 0.9 |
| vertVariance | 1 |
| horizJitter | 1 |
| minDelay | 2 ms |
| maxDelay | 5 ms |
| rfKey | "p" |
| minHold | 25 ms |
| maxHold | 40 ms |
| minGap | 65 ms |
| maxGap | 90 ms |

Rounding: `Round(num) = math.floor(num + 0.5)` — replicate as `Math.Floor(num + 0.5)`.

## Roster integrity (atomatically checked in tests)
The integrity test regex-parses the source fixture and asserts the JSON catalogue matches
**1:1**: same operator list in same order, same side label, same number of weapons per operator,
same weapon order per operator, same `name`/`pull`/`rapidFire` per weapon. Counts asserted by name:
attackers + defenders + total weapons all come *from the parsed source*, never hardcoded by hand.

## Behavioral semantics to preserve
- [x] MB3 (middle) toggles enable/disable
- [x] MB5 (XButton2/forward) with Caps ON → next weapon in selected op; Caps OFF → next op, weapon resets to 1
- [x] MB4 (XButton1/back) with Caps ON → previous weapon; Caps OFF → previous op, weapon resets to 1
- [x] LMB held → run behavior while held; released → stop promptly
- [x] Wraparound at both ends for ops and weapons (Lua: `(i-2+n) % n + 1`)
- [x] Effective pull = Round(pull × recoilMultiplier), 0 → no vertical move
- [x] Vertical variance `±vertVariance`, horizontal jitter `±horizJitter` per tick
- [x] rapidFire=true loop: press rfKey, Sleep(random minHold..maxHold) incl. endpoints, release,
      optional pull move, Sleep(random minGap..maxGap); repeat until LMB released
- [x] rapidFire=false loop: pull move, Sleep(random minDelay..maxDelay); repeat until LMB released
- [x] `math.random(min,max)` → inclusive uniform `Next(min, max+1)`
- [x] All status info preserved in UI (multiplier, rf key, side, op, weapon index/total, mode,
      effective pull, timing range)

## Known quirks preserved verbatim (see ARCHITECTURE.md)
- Solis `SMG-11 (Sec)` pull=48; Tachanka `Bearing 9 (Sec)` pull=34 while Echo/Thunderbird use 49.
