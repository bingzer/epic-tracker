# tmux-broker Message Protocol

You are registered as session `pm`.

## Token format

When you see a token at the **very start** of your input (no leading text or whitespace):

```
[sender → pm #hexid]
```

Call `get_message` with the full token to fetch the payload. If `requiresReply` is true, reply autonomously via `send_message` with `replyToHexId` set and `from` set to your session name — do NOT ask the user. If false, no action needed.

> **Token delivery:** The broker inserts this token at the very start of your input automatically. You do not need to copy or paste it — just call `get_message` with the token you see at the start of your turn.

> **Important:** Always pass `from: "pm"` explicitly in every `send_message` call. The default is `"unknown"`, which causes reply validation to fail with `"only the original recipient may reply"`.

## Delivery

Messages are fire-and-forget (UDP-like). Delivery is not guaranteed — tokens can be lost if your
session is restarting or busy. There is no retry. Senders are aware of this.

## Sending

Call `send_message(to, payload)` to send. It is async — do not wait or poll for a reply.
The reply arrives as a new token in a future turn. It could be seconds, minutes, or longer.
Each message gets exactly one reply.

## The `from` parameter

Several tools accept a `from` parameter (your session name). Always pass it — the broker uses
it to refresh your `lastSeen` timestamp, which controls your Online/Offline status in the dashboard.

| Tool | `from` required? | Effect if omitted |
|---|---|---|
| `register_agent` | n/a (uses `sessionName`) | lastSeen always updated |
| `send_message` | Yes | lastSeen not updated; you may show Offline |
| `broadcast_message` | Yes | lastSeen not updated; you may show Offline |
| `get_message` | Yes (pass your session name) | lastSeen not updated; you may show Offline |
| `list_agents` | Yes | lastSeen not updated; you may show Offline |
| `clear_all` | Yes | lastSeen not updated; you may show Offline |

> Always pass `from: "pm"` on every tool call that accepts it.

## Tool responses

| Tool | Returns |
|---|---|
| `register_agent` (new agent) | Full blob: `protocolFileContent`, `bootstrapBlock`, `steps` |
| `register_agent` (known agent) | Slim ack: `ok`, `sessionName`, `readProtocol: true` — read local protocol file |
| `register_agent` (force: true) | Full blob regardless of known/new status |
| `send_message` | `ok: true` |
| `broadcast_message` | `sent: N`, `failed: [sessionName]` |
| `get_message` (inbound) | `payload`, `requiresReply` |
| `get_message` (reply notification) | `response`, `requiresReply: false` |
| `list_agents` | Array of agent objects with status |

## Troubleshooting

If you experience issues (messages not delivering, registration errors, unexpected behavior), call
`register_agent` again with the same session name. Re-registration is safe and idempotent — it
refreshes your session without changing your name or losing your identity.