# Diagnostics

ToolRill logs startup, tool discovery and calls, connection events, errors, and
shutdown. Add a root `diagnostics` section beside `hosts` in `toolrill.json`.
It applies to every host using that file, including manager child processes:

```json
{
  "diagnostics": {
    "enabled": true,
    "level": "INFO"
  }
}
```

| Setting | Purpose |
| --- | --- |
| `enabled` | Turns diagnostic logging on or off; defaults to `true`. |
| `level` | `INFO` logs all levels; `WARN` logs warnings and errors; `ERROR` logs errors only. Defaults to `INFO`. |

Example diagnostic record (one JSON object per line):

```json
{"timestamp":"2026-10-15T17:21:21Z","level":"INFO","server":"local","message":"Connected.","category":null,"eventId":null,"exception":null,"traceId":null,"spanId":null}
```

Core diagnostics and .NET host/SDK logging always write to **stderr**, for
both stdio and HTTP hosting. Default framework logging providers are replaced
by the same redacting provider. Stdio stdout is reserved for MCP messages even
when `diagnostics.enabled` is `true`. CLI startup status and command errors
remain separate, redacted human-readable stderr output; they are not disabled
by `diagnostics.enabled`. Manager command summaries remain on stdout.

Records include severity, optional server, framework category/event ID and
exception text. Existing `System.Diagnostics.Activity.Current` trace/span IDs
are included when available; no activities, exporters or telemetry backend are
created. JSON escaping keeps multiline messages inside a single record.

## Sensitive Values

Before output, messages, server/category names and exceptions (including inner
exceptions) pass through the shared redactor. Recognized credential assignments
are replaced with `[REDACTED]`: Authorization/Proxy-Authorization, API keys,
tokens, passwords, secrets, signatures and credentials, plus Cookie/Set-Cookie
headers. Matching is case-insensitive and supports quoted JSON/key-value text,
including escaped quotes. Unquoted credential values are conservatively removed
through the next delimiter; adjacent descriptive text may also be redacted.
Standalone Bearer/Basic credentials are also redacted. HTTP(S) URL user info,
**all query values** (including custom parameter names) and fragments are
redacted, preserving the address/path and query names. On a regex processing
timeout, the whole message is replaced with `[REDACTED]`.

ToolRill additionally registers resolved outgoing HTTP header values and the
environment values substituted into them for exact, case-sensitive redaction,
even with arbitrary custom header names or unlabeled exception text. Values
remain in memory for the process lifetime so later errors are still protected.
Validation errors omit configured URLs and header values.

Other protection is pattern-based, not detection of arbitrary unlabeled or
encoded/transformed secrets. Keep credentials out of tool names/descriptions and review
logs before sharing. MCP tool arguments/results are not rewritten; third-party
process output and independently configured logging sinks are outside this
redaction boundary. File logging and telemetry exporters are not configured.

See [Server Configuration](configuration.md) for server setup and environment overrides.