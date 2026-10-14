# Auto Mahjong Solver

A Dalamud plugin that reads FFXIV Doman Mahjong, computes the mathematically best discard and call, shows a suggestion overlay, and (optionally, behind a gate) plays for you.

Command: `/amjs` (alias `/mahjong`).

## Status

The solver engine, board reading, and the autonomous play loop are live-verified on casual tables
(multi-hand sessions without stalls). Riichi declaration and the defense layer are implemented and
await their first observed in-game trigger.

| Capability | State |
|---|---|
| Solver engine (shanten, ukeire) | Implemented, unit-tested |
| Yaku / Doman scoring | Implemented, unit-tested; han→points table pending live verification |
| `Emj` hand reader (closed hand, aka) | **Verified live** (incl. under popups) |
| `Emj` meld reader (chii/pon/kan, feeds meldCount) | **Verified live** — meld strips mapped (self=112 family); post-call discard state 28 handled |
| `Emj` addon reader (rivers, dora, winds, scores, riichi badges) | Implemented; opponent river↔panel pairing pending live verification (`/amjs state`) |
| Seen-tile ukeire + furiten avoidance + fold vs riichi | Implemented; fold path awaits first observed opponent riichi |
| Auto-riichi (declare on live closed tenpai, then tsumogiri) | **Verified live** |
| Auto-play loop (discard, win, calls, redeal prompt, result screens) | **Verified live** — multi-hand sessions; popup fires retry-verified; settle grace + stall watchdog |
| Nine-terminals redeal prompt (keep on kokushi shanten ≤ 2) | Implemented; option label match pending first observation |
| Value-aware discard EV (exact points EV at tenpai; dora/aka/yakuhai retention above) | Implemented, unit-tested |
| Shared turn policy (engine `TurnPolicy`, used by bot and simulator) | Implemented |
| Self-play simulator (`tools/AutoMahjongSolver.Simulator`) | Implemented; baseline below |
| Calling strategy v1 (pon: yakuhai / secured-yaku / dora-tanyao; chi+kan never) | Implemented, unit-tested, simulated; pending live verification |
| Suggestion overlay | Planned |
| Chi / kan calls | Planned (chi variant-selection UI unverified; kan flips unreadable dora) |

## Layout

- `AutoMahjongSolver.Engine` — pure C# (`net10.0`), no Dalamud dependency. Tiles, shanten, ukeire, and (later) yaku/scoring. Fully unit-testable offline.
- `tests/AutoMahjongSolver.Engine.Tests` — xUnit tests for the engine.
- `AutoMahjongSolver` — the Dalamud plugin (planned): `Emj` reader, suggestion overlay, gated actuation. Built on ECommons.

## Build

```
dotnet test                                                   # engine + tests (no game required)
dotnet run --project tools/AutoMahjongSolver.Simulator -c Release -- 2000 7      # self-play: matches, seed
dotnet run --project tools/AutoMahjongSolver.Simulator -c Release -- ab 2000 7   # candidate vs baseline
```

The simulator runs matches in parallel; each match draws its walls from its own stream derived
from the seed, so results are reproducible regardless of core count.

Self-play baseline (2000 matches, seed 7, all four seats on the current policy, 2026-10-14):
11067 hands, ~17.5% win rate per seat-hand, ~9.3% deal-in, ~30.6% riichi, 29.9% exhaustive draws,
average win 6199 points / 3.39 han.

Self-play is symmetric, so it cannot show whether a change wins more points. Judge policy changes
with `ab`: point `Policies.Candidate` (tools/AutoMahjongSolver.Simulator/Policies.cs) at the
variant, and the candidate takes each seat in turn against three baselines on the same walls. It
reports the candidate's score against the table average and its average placement, each with a
standard error; identical policies score exactly 0. As a calibration point, a candidate that never
folds against riichi scores -457 ± 120 per match over 2000 matches.

The Dalamud plugin project builds against `Dalamud.NET.Sdk` and requires a Dalamud dev environment.

## Notes

Doman Mahjong is standard 4-player Japanese riichi with FFXIV-specific scoring: a flat han-to-points table with **no fu calculation**. The engine targets this ruleset.

Auto-play of a ranked, human-PvP minigame carries account risk. Auto-play only acts at recognized
Duty Finder tables (checked by ContentFinderCondition row). Ranked tables are refused unless
"Auto-play on ranked tables" is enabled in `/amjs config`; unrecognized tables are always refused.
