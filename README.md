# MahjongXIV

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for FFXIV's Doman Mahjong. It reads the
table, works out the best discard and call, and can either point at the move for you or play the
hand itself.

- **Suggest mode**: shows the best discard, the alternatives, your shanten and your waits. It can
  also draw a marker over the recommended tile on the board. It never sends an input.
- **Auto-play mode**: discards, declares riichi, tsumo and ron, takes or passes calls, answers the
  nine-terminals redeal prompt, and advances result screens. It waits a random, configurable time
  before each action.

Doman Mahjong is standard four-player Japanese riichi with one FFXIV twist: points come from a flat
han table with **no fu**. The engine is written for that ruleset.

## Install

1. In game, open `/xlsettings` → **Experimental**.
2. Under **Custom Plugin Repositories**, add this URL, tick **Enabled** and save:

   ```
   https://raw.githubusercontent.com/kuchris/DalamudPlugins/main/repo.json
   ```

3. Open `/xlplugins`, search for **MahjongXIV** and install it.

## Usage

Open the window with `/mj` (or `/mahjongxiv`), pick **Suggest** or **Auto-play**, and press
**Start**. Then join a Doman Mahjong table and the plugin takes over from there.

| Command | Effect |
|---|---|
| `/mj` | Toggle the main window |
| `/mj suggest [on\|off]` | Start or stop Suggest mode (toggles with no argument) |
| `/mj auto [on\|off]` | Start or stop Auto-play (toggles with no argument) |

Diagnostic subcommands (`dump`, `tree`, `board`, `state`, `melds`, `values`, `options`, `capture`,
`probe`, `diag`, `events`, `buttons`, `click-discard`, `discard`, `advance`) write to the Dalamud
log (`/xllog`). They exist for reverse-engineering the table UI, and most players won't need them.

### Settings

You'll find these under **Settings** in the main window:

| Setting | Default | Notes |
|---|---|---|
| Auto-play on ranked tables | Off | Lets auto-play act at ranked tables (see below) |
| Highlight the suggested tile | On | Suggest mode only. **Style**: Brackets, Arrow or Glow. **Colour**: one of seven palettes |
| Auto-play delays | Discard 0.7–1.8 s, Call 0.5–1.2 s, Results 1.5–2.8 s | Each action waits a random time in its range |

### Tables and safety

Auto-play only acts at tables it recognizes. It checks the Duty Finder entry:

- **Casual** tables (full or quick match, with kuitan on or off): allowed.
- **Ranked** tables: refused unless you turn on *Auto-play on ranked tables*. Even then, the plugin
  leaves result screens to the game's own timer, because advancing them early has crashed the
  client.
- **Anything else**: always refused.

Suggest mode works at every table.

> [!WARNING]
> Automating a minigame where you play against real people is against the FFXIV terms of service
> and may put your account at risk. Use auto-play at your own risk, especially at ranked tables.

### Known limitations

- Use the default tile design and an English client. With other tile designs the hand can't be
  read, and on other client languages the seat wind and riichi state may be misread.
- Auto-play never calls chi or kan. It only takes a pon when the hand already has a yaku, the
  tile is yakuhai, or it is a dora/tanyao hand.

## Building from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). Building the plugin also needs a
Dalamud dev environment (XIVLauncher with Dalamud installed, or `DALAMUD_HOME` on Linux). The engine
and its tests don't need that.

```bash
dotnet build MahjongXiv.slnx -c Release
```

```bash
dotnet test MahjongXiv.slnx
```

The plugin zip is written to `MahjongXiv/bin/x64/Release/MahjongXIV/latest.zip`. To try a local
build, add the output folder under **Dev Plugin Locations** in `/xlsettings` → Experimental.

| Project | Contents |
|---|---|
| `MahjongXiv.Engine` | Pure C# solver with no Dalamud dependency: tiles, shanten, ukeire, yaku, Doman scoring, discard EV, call policy, defense |
| `MahjongXiv` | The Dalamud plugin: reads the `Emj`/`EmjL` table addon, runs the overlay and window, drives auto-play |
| `tests/MahjongXiv.Engine.Tests` | xUnit tests for the engine |
| `tools/MahjongXiv.Simulator` | Self-play simulator for evaluating policy changes |

### Simulator

```bash
dotnet run --project tools/MahjongXiv.Simulator -c Release -- 2000 7
```

```bash
dotnet run --project tools/MahjongXiv.Simulator -c Release -- ab 2000 7
```

The first command runs self-play with every seat on the current policy, using 2000 matches and
seed 7. The second runs an A/B test. Its candidate policy comes from
`tools/MahjongXiv.Simulator/Policies.cs`, or from `key=value` overrides of `EvalOptions`. The
candidate plays each seat in turn against three baseline players on the same walls. The result is
the candidate's score against the table average, with a standard error. Two identical policies
score exactly 0.

Results are reproducible for a given seed. Judge policy changes with `ab`. Self-play alone can't
show whether a change scores more points.

Live findings about the game UI (node ids, callback opcodes, table states) are in
[docs/LIVE-VERIFICATION.md](docs/LIVE-VERIFICATION.md).

## Credits and license

MahjongXIV is a fork of [FFXIV-AutoMahjongSolver](https://github.com/XeldarAlz/FFXIV-AutoMahjongSolver)
by Xeldar Alz. This fork adds a new compact interface, faster solver internals, discard lookahead
and ranked-table handling.

It is licensed under the [GNU AGPL v3.0 or later](LICENSE.md), like the original. See [NOTICE](NOTICE)
for the copyright notices.
