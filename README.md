# XIV Blackjack

Dealer-side blackjack tooling for FFXIV venues, as a Dalamud plugin.

Stripped down from [DeathRoll Helper](https://github.com/Infiziert90/DeathRoll) by Infi
(via [caitlyn-gg's fork](https://github.com/caitlyn-gg/DeathRoll)). Everything except the
blackjack engine has been removed — no dice modes, tournaments, Tic-Tac-Toe, Minesweeper,
Uno, Peggle, or Bahamood; added more blackjack functionality like player banking and payout manager, a full Blackjack suite

## What's here

- **Registration** — target a player and add them to the table
- **Per-player bets** — editable in the registration table, defaults configurable
- **Player actions** — Hit, Stay, Surrender, Double Down, Split
- **Dealer rules** — Hard 16/17, Soft 16/17
- **Two modes** — Normal (plugin draws cards) and Venue (players roll `/random 13` for their own cards)
- **Payout calculation** — with a "Copy Payout" button for pasting into chat
- **Blocklist** — exclude specific players from joining
- **Player banking** — a balance held with the dealer absorbs wins and losses, so gil only moves on deposit and cash-out
- **Payout manager** — end-of-night reconciliation for self-bankrolled and venue-bankrolled tables
- **Trade helper** — targets the player, opens the trade and fills in the amount; payouts over the 1,000,000 cap are chained automatically

## Commands

`/blackjack` — open the main window
`/blackjack config` — open settings

## Installing

In game: `/xlsettings` → **Experimental** → under **Custom Plugin Repositories**, paste

```
https://raw.githubusercontent.com/NDokahara/FinalFantasyXIV/main/repo.json
```

tick it, **Save and Close**, then `/xlplugins` → search **XIV Blackjack** → Install.

That URL is the plugin list, shared by everything in
[NDokahara/FinalFantasyXIV](https://github.com/NDokahara/FinalFantasyXIV). Add it once and
future plugins show up on their own.

## Upgrading from 0.1.x

0.2.0 renamed the plugin's internal identifier, so Dalamud sees it as a new plugin rather
than an update. Settings, bank balances and blocklists do not carry over. Settle any
outstanding balances before switching, then uninstall the old **XIV Blackjack** entry and
install this one.

## Building

Requires the Dalamud dev environment (`%AppData%\XIVLauncher\addon\Hooks\dev`).

```
dotnet build -c Release
```

See [RELEASING.md](RELEASING.md) for packaging and publishing.
