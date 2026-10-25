# SquashServer — online play

The relay server for re:SquashStrike's online play (P2-M8). It pairs players
by private room code, quick match or the ranked ladder, and relays their
inputs. It also steps its own copy of every match from those inputs, so a
result and a rating change never depend on what a client says.

Plan: `SQUASH FRONTEND MODIFIED/RE_SQUASHSTRIKE_P2M8_ONLINE_PLAN.md`.

## Run it locally

```bash
dotnet run -c Release --project Tools/SquashServer
```

It listens on port 5180. To test against it, set the game's Settings > Gameplay > Online server to
`ws://localhost:5180/ws`. Data goes in `data/squash.json`, with ranked
replays in `data/replays/`.

| Path | What |
|---|---|
| `/ws` | the game's WebSocket (binary `SquashSim.Net.NetMessages`) |
| `/` | health: version, sim fingerprint, players online |
| `/leaderboard?id=<player id>` | the top 100, and you |
| `/player/<player id>` | one player's rating and record |

## Tests

```bash
dotnet run -c Release --project Tools/SquashServer.Tests
```

The last line is `all passed`. The tests cover:
- Elo;
- rooms and queues;
- relay order;
- a ranked match played out, where the server's winner matches both clients';
- a client lying in its rally hashes, which ends the match as a desync;
- a forfeit after the 20 s grace;
- reconnecting mid-match;
- real WebSockets.

## Deploy to Fly.io

Do this once, from the repository root. It needs your own Fly.io account; `fly auth login` opens the browser.

```bash
fly auth login
fly apps create re-squashstrike          # or another free name (then change `app` in fly.toml)
fly volumes create squash_data --size 1 --region syd -a re-squashstrike
fly deploy . --config Tools/SquashServer/fly.toml --dockerfile Tools/SquashServer/Dockerfile
```

After that, rerun only the last line for each update. Then, in the game, go to
Settings > Gameplay > Online server and enter
`wss://re-squashstrike.fly.dev/ws`.

**Keep the server and the game in step.** The server refuses a game whose sim
fingerprint differs from its own. Redeploy whenever a game release changes the
sim (physics, tuning, rules).
