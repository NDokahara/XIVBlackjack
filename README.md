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

## Reporting bugs

Something off at the table? Either works — the **General** tab in settings has buttons for both.

- [Bug report form](https://forms.gle/9wa27ViAzpPZ3aAL8) — quick, no account needed
- [GitHub issues](https://github.com/NDokahara/XIVBlackjack/issues) — best if you want to attach screenshots or logs

Mention your plugin version (shown next to the buttons) and the steps that led to it.
