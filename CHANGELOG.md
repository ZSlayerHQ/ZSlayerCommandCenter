# Changelog

## 2.22.4 — Flea market safety

A safety release for populated servers. It fixes four defects in flea regeneration.
It does **not** change what any multiplier evaluates to — see "Not fixed in this
release" below.

### Fixed

**Regeneration no longer delists player offers.** *(critical)*
Every regeneration cleared the entire offer holder before rebuilding it, but the
rebuild only recreates fake-player offers. Player listings could not come back —
SPT loads them once per server start — so each regeneration permanently destroyed
live player listings until restart. Regeneration now removes only fake-player
offers; player, trader and unattributed offers are preserved. The log reports the
split.

Trader offers were never permanently lost (they return at the next resupply), but
clearing them emptied the market in the meantime.

**The price table can no longer be built from two configurations.**
The apply loop re-read the live config once per template, so a config save landing
mid-loop produced a table built from a mix of old and new values. The whole pass now
resolves against one detached snapshot. `GetConfig()` returns a real deep copy
instead of the live object it previously handed out while documenting itself as a
copy.

**Flea routes no longer return `200` on failure.**
Every route discarded the regeneration result and reported success — including
`POST /flea/regenerate`. Routes now return a uniform contract:

```json
{ "persisted": true, "applied": false, "marketState": "partial-or-unknown",
  "error": "...", "guidance": "..." }
```

- `200` — the live market matches the persisted config
- `400` — rejected before anything changed
- `409` — nothing was touched (a regeneration was already running)
- `500` — live state may sit between two configurations

There is deliberately **no rollback**. By the time a failure can surface, globals,
prices and the offer holder may each be part-updated; reverting `config.json` alone
would report an undo that did not happen. The response says what state the market is
in instead.

**Input is validated before it reaches disk.**
`POST /flea/config` stored whatever it was given — a null dictionary, a NaN
multiplier or an unknown category key went to `config.json` and threw on the next
lookup. All flea routes now reject unknown categories, unknown template IDs,
non-finite numbers, out-of-range values and inverted min/max pairs.

**Regeneration is serialised.**
The offer holder's indexes are `HashSet`s inside a `ConcurrentDictionary` — safe to
look up concurrently, not safe to mutate concurrently. Two overlapping regenerations
could leave an offer present but missing from the index searches read, so a listing
existed that nobody could find. The whole apply → remove → generate transaction now
holds one lock, and a second concurrent regeneration is rejected with `409` rather
than queued behind a pass that may be mid-mutation.

### Not fixed in this release

- **The flea buy multiplier is still applied twice.** A configured `2.0` yields
  roughly `2.0x` at server start and drifts toward `4.0x` as offers are replaced.
  The fix ships in 2.23.0.
- **`POST /flea/config` and the flea-expansion routes still do not regenerate.** A
  restart or an explicit `POST /flea/regenerate` is still required after calling them
  directly. They will now tell you when something failed.
- **Regeneration is still not coordinated with SPT's own flea update.** CC-versus-CC
  overlap is closed; CC-versus-SPT is reduced — selective removal touches far fewer
  offers than the old full clear — but not eliminated. Prefer regenerating when the
  server is quiet.

### Upgrading

Full deploy, not DLL-only. `res/commandcenter.html` changed and is read from disk at
runtime, so a DLL-only copy leaves a 2.22.4 backend serving an older dashboard.
