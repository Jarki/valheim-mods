# Server Metrics

**Unpublished release candidate for the Jarki team.** Mod-manager instructions apply after an approved publication; until then, import a locally built ZIP or use the manual instructions.

A **dedicated-server-only** Prometheus exporter. No client mod is required. Exposes connected players/sessions, connected playtime, save outcomes, retained native backups, loaded plugin metadata, and durable per-character connected-time/session/death totals. It is an original implementation, not Valheim Stats code.

Playtime includes AFK time. Deaths are observed vanilla client notifications, not anti-cheat-verified events; kills/damage are not inferred. Character names and opaque profile IDs appear in metrics and the private ledger, but Steam usernames/account IDs are not read or exported. Obtain appropriate player consent before sharing dashboards.

## Requirements and installation

Requires a Valheim dedicated server and **BepInExPack Valheim 5.4.2351** on that server. Do not install this into client profiles.

- **Mod manager:** if your server's existing deployment uses a Thunderstore-compatible manager, install `Jarki-Server_Metrics` into its **server** profile and accept the BepInEx dependency. This does not create or configure a server.
- **Manual:** stop your server using its normal administrative procedure. With an appropriate existing BepInEx installation, copy `plugins/ServerMetrics/` from the archive into the server's `BepInEx/plugins/`, producing `BepInEx/plugins/ServerMetrics/ServerMetrics.dll`. Preserve `LICENSE`, remove duplicate DLLs, then start through the existing server loader route. Linux needs the appropriate Linux loader. No client or firewall changes happen automatically.

## Configuration and scraping

After first load, edit `BepInEx/config/local.valheim.servermetrics.cfg` with the server stopped:

```ini
[Exporter]
BindAddress = 127.0.0.1
Port = 9101
AllowedScraperAddress =
ModpackManifestPath =
```

Defaults permit loopback scraping only. Add `http://127.0.0.1:9101/metrics` as a Prometheus target **on the same host**; inside another container/machine, loopback refers to that environment instead. Prometheus and Grafana are separate, optional tools, not bundled services.

Settings apply at startup. For remote scraping, use a private reachable interface, one literal `BindAddress` and one explicit `AllowedScraperAddress` IP. Wildcard/unspecified/multicast bindings are rejected; proxy headers do not bypass peer filtering. **Plain HTTP: no TLS or user authentication. Never expose this endpoint publicly.** IP filtering is not a substitute for network isolation.

`CharacterPlaytimePath` defaults to `BepInEx/data/ServerMetrics/character-playtime.json` in the active server installation. Keep this writable, persistent and outside world saves; back it up separately. To change the path, stop the server and deliberately move the ledger before restarting. Corrupt/oversized ledgers fail explicitly rather than silently resetting. Background persistence is approximately once per minute; crashes can lose updates since the last successful write. Existing playtime-only ledgers retain totals with deaths initially zero.

Leave `ModpackManifestPath` empty unless you provide a supported manifest; it is optional and not needed for runtime-loaded plugin metrics. No modpack, dashboard, import code or external service configuration is bundled. See the source documentation for the metric catalog and optional manifest interface.

`GET /metrics` (also `/metrics/`) serves bounded Prometheus text; `HEAD` returns headers. HTTP bind failures retry after one minute, then five, then every thirty minutes without resetting collected totals. Invalid configuration/game observation failures disable the endpoint instead of reporting healthy stale observations. Monitor Prometheus `up`, observation freshness, inventory scan success and character storage success. Check `BepInEx/LogOutput.log` for errors.

## Semantics, uninstall and save safety

Aggregate connection/save counters reset at exporter startup; use Prometheus rates/increases. Character totals persist separately and begin at installation, without historical backfill. Identity is a client-reported character profile key, not an account verification system. Backup metrics describe game-reported outcomes and native chunked-world backup metadata, **not independent integrity checks or proven restores**. The exporter never copies/restores saves or changes world contents.

To uninstall, stop the server and remove `BepInEx/plugins/ServerMetrics/ServerMetrics.dll`. Retain the config/ledger if you want totals after reinstalling; delete them only deliberately. Removing the plugin does not remove Prometheus history or dashboards. Back up server worlds and the separate ledger before upgrades; server operations require your own authorization and maintenance plan.

## Testing and limitations

The development record contains standalone regression groups, source/assembly inspection, HTTP/persistence smoke checks and historical server observations. Constructed notification tests are not real multiplayer death tests. No new server was launched for this packaging work, and current-machine live-server behavior has not been established. Broader game-version/modpack compatibility, real multiplayer death accounting and clean-profile archive deployment are not newly verified. This is an unpublished first Thunderstore package, not a claim of production readiness.

## License, credits and AI disclosure

Original code and package icon: **MIT, copyright 2026 Jarki**; see `LICENSE`. Valheim, BepInEx, Prometheus and Grafana belong to their respective owners; their binaries/services are not bundled. Developed with AI assistance, including OpenAI Codex. This packaging work used **gpt-6.1-sol**; historical work is not all attributed to that model. The icon is original programmatic artwork.

## Source and support

[Source and development documentation](https://github.com/Jarki/valheim-mods/tree/main/mods/ServerMetrics) · [Report issues](https://github.com/Jarki/valheim-mods/issues). Include game/mod versions, reproduction steps and relevant redacted BepInEx logs; never post passwords, tokens or private saves.
