Version: 5

# tmux-broker Protocol — epictrackerdev

## Inbound tokens

```
[sender → epictrackerdev #hexid]   ← direct message
[sender → @channel #hexid]        ← channel post (you are a member of @channel)
```

For direct messages: call `get_message(token)`. If `requiresReply: true`, reply autonomously with `send_message(to: sender, replyToHexId: hexId, from: "epictrackerdev", payload: ...)`. If false, no action.

For channel posts: call `get_message(token, from: "epictrackerdev")` — the `from` param is required so the broker can verify membership. `requiresReply` is always false for channel posts.

## Outbound

- Direct: `send_message(to, payload, from: "epictrackerdev")`
- Channel post: `post_to_channel(channelId, payload, from: "epictrackerdev")`
- Channel setup: `create_channel`, `invite_to_channel`, `list_channels`, `leave_channel`

## Reaching the human

`human` is a reserved, always-valid session name backed by the broker UI — not a tmux pane.

- To contact Ricky directly: `send_message(to: "human", from: "epictrackerdev", payload: ..., requiresReply: true)`
- Set `requiresReply: true` when you need a response. Ricky sees the message in the UI inbox and replies from there.
- Channel posts from Ricky arrive as `[human → @channel #hexid]` — handle them like any other sender.
- Never try to `start_agent` or `stop_agent` for `human` — it has no tmux session.

## Batch Fetch Mandate

Never call `get_message` more than once per turn. Collect ALL broker tokens present in the current input, then pass them as a single array to one `get_message` call. Looping over tokens with separate calls is forbidden — dropped or duplicate delivery may result.

## Channel Reply Rule

When `get_message` returns `replyToChannel: true`, you MUST reply via `post_to_channel(channelId, payload, from: "epictrackerdev")` using the returned `channelId`. Never use `send_message` or plain CLI output to reply to a channel message — the reply will not reach channel members.

## Rules

- Always pass `from: "epictrackerdev"` on every tool call that accepts it — keeps your lastSeen fresh.
- Replies are point-to-point; channel tag is preserved in the reply token automatically.
- For channel messages, the token recipient is the channel itself (`@channel`), not your session name — the broker delivers it to all members.
- Re-registration is safe and idempotent — call `register_agent` any time to recover.
- On every `register_agent` call, read the `Version:` line from this file (regex `^Version: (\d+)`) and pass it as `myVersion`. The broker returns the full protocol blob automatically if your version is outdated.