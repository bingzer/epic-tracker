# Epic Tracker Wishlist

| # | Feature | Difficulty | Status | Open Questions |
|---|---------|------------|--------|----------------|
| 1 | **Open chat button** — in dashboard for any agent involved in an epic. Opens Claude Code CLI for that agent's session. | Easy | ✅ Done | Uses `openterm:<agentId>` href on epic agent, each coding agent chip, and reviewer. |
| 2 | **Nudge PM epic agent button** — appears on every state. State-aware message: `drafting` uses existing wake message; all other states send "Hey, are you still working on epic `{id}`? If not, please continue where you left off." | Easy | ✅ Done | — |
| 3 | **Absolute paths everywhere** — `EpicDocumentPath` and `EpicGovernancePath` now absolute via `EpicTracker:EpicsBasePath` in `appsettings.json`. | Medium | ✅ Done | — |
| 4 | **Add/remove coding agents and reviewer in dashboard** — chip editor + input on the epic detail page. | Easy | ✅ Done | — |
| 5 | **Force spec to any state** — admin dropdown on spec row, no guard rails, sets state directly. | Easy | ✅ Done | — |
| 6 | **View markdown docs in dashboard** — right-side drawer, renders markdown via `marked`. Clickable on epic doc, governance, and spec doc paths. | Medium | ✅ Done | — |
| 7 | **Delete epic** — hard delete with `window.confirm`. Removes from list via SignalR. | Easy | ✅ Done | — |
| 8 | **MCP search epic by name** — new MCP tool, agents don't have to list all. | Easy | 🔲 Todo | Low priority. |
| 9 | **New epic on top immediately** — prepend + sort on create; SignalR upsert on `EpicUpdated`. | Easy | ✅ Done | — |
| 10 | **Show spec status in spec row** — already done (StateBadge in state column). | Easy | ✅ Already done | — |
| 11 | **Show all epic properties in epic details** — all fields visible: Slug, CreatedAt, HumanInLoop/AgentSwarm as collapsible JSON blocks. | Medium | ✅ Done | — |
| 12 | **Tabbed epic detail page** — tabs: `Epic Details` / `Epic Board` / `Audit Log` / `Agent`. Human review above tabs. Agent swarm in Agent tab. | Medium | 🔲 Todo | — |
| 13 | **Breadcrumbs during transient states** — shows full trail with last real state highlighted + amber pill for transient. Derived from audit log. | Medium | ✅ Done | — |
| 14 | **General UI improvements** — ongoing, opportunistic. | — | 🔲 Ongoing | — |
| 15 | **Peer review flag per spec** — `IsPeerReviewRequired` bool on each spec. When set, after coding is done, all other coding agents in the epic (not the implementer) must review the spec before it advances to code_review/AC. New `peer_review` spec state. Epic agent coordinates via tmux. | Medium | 🔲 Todo | What happens on peer review failure — back to coding? Should final sign-off rejection also reset peer review? |
