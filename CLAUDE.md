# CLAUDE.md

## About This App

Epic Tracker is your app. You are the owner. It is a standalone MCP server + web dashboard that owns the state machine for multi-agent software epics. It tracks where every epic and every spec is in the governance process, enforces gate conditions in code, and persists all state in SQLite.

**Stack:** C# .NET 10, SQLite via EF Core, ASP.NET minimal API, MCP via ModelContextProtocol.AspNetCore, SignalR, React 19 + Vite + Tailwind 4.

**Project structure:**
```
EpicTracker/        — EF entities, DbContext, state machine, EpicService
EpicTracker.Api/    — ASP.NET minimal API, Epic Agent MCP, SignalR hub, REST endpoints, dashboard
EpicTracker.Tests/  — xUnit tests
EpicTracker.UI/     — React + Vite + Tailwind dashboard
```

**MCP endpoint:**
- `http://127.0.0.1:6790/mcp` — Epic Agent tools: `get_epic`, `advance`, `set_epic_flags`, `raise_agent_swarm`, `submit_agreement`, `raise_human_in_loop`, `create_spec`, `get_spec`, `advance_spec`, `update_spec`

**Human-in-loop wake-up pattern:**
After a human approves/rejects via the dashboard (`POST /epics/{id}/approve-human-in-loop`), the API must send tmux `send-keys` to the epic agent's session to wake it. The agent is blocked waiting — it does not poll. No SignalR on the agent side.

**Known gaps:**
- No auth on REST or MCP endpoints

## Dev run

Epic Tracker runs as a persistent local background service — same pattern as tmux-broker. The published DLL runs hidden; agents connect via MCP over HTTP. SQLite DB is `epic-tracker.db` in the `publish/` directory.

**Deploy (after any code change):**
```powershell
.\scripts\deploy.ps1
```
Kills any existing process on :6790, builds the UI (`npm run build`), does `dotnet publish`, starts the published DLL as a hidden background process, waits for `/health` to confirm it's up.

**Restart without rebuilding:**
```powershell
.\scripts\start.ps1
```

**Active UI iteration only (two terminals):**
```powershell
# Terminal 1 — keep the published service running, or:
cd EpicTracker.Api; dotnet run

# Terminal 2 — HMR via Vite dev server
cd EpicTracker.UI; npm run dev
```

**Ports:**
| | Port |
|---|---|
| API / dashboard / Epic Agent MCP | http://localhost:6790 |
| Vite dev server (npm run dev) | http://localhost:6791 → proxies to 6790 |

**How the UI gets served:** Vite builds into `EpicTracker.Api/wwwroot/`. The API serves it via `UseStaticFiles()` + `MapFallbackToFile("index.html")`. Port binding for published builds is set in `appsettings.json` (`"Urls": "http://127.0.0.1:6790"`).

## MCP registration

Add to `~/.claude/settings.json`:

```json
{
  "mcpServers": {
    "epic-tracker": {
      "type": "http",
      "url": "http://127.0.0.1:6790/mcp"
    }
  }
}
```

## Startup

At the start of every new session, call `mcp__tmux-broker__register_agent` with `sessionName: "epictrackerdev"` and `cwd: "<path to this repo>"`. Do this before anything else.

## tmux-broker

If you see a token matching `[... → epictrackerdev #......]` at the start of your input, it is a broker message. The broker inserts it automatically — you do not copy/paste it. Read `tmux-broker-message-protocol.md` in this directory for instructions.
