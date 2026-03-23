# Architecture Improvements

## 1. Machine-wide backend catalog
Add a machine-level backend catalog under local app data. Each backend registration update must also upsert a catalog record that contains:
- backend identity
- workspace root
- settings path/hash
- base URL
- manager URL
- auth token
- pid and start time

The manager layer will read from this catalog, probe live backends, and prune stale entries.

## 2. Aggregated manager status
Change the manager status API from "current backend only" to an aggregate snapshot that includes:
- current backend identity
- discovered backends
- per-backend sessions
- per-backend operations
- aggregate counts

## 3. Manager action proxy
Add manager actions that can target any discovered backend:
- stop session
- force stop session
- rebuild / restart session
- start default app
- trigger workspace build

Action routing rules:
1. If the target backend is the current backend, execute locally.
2. If the target backend is remote, proxy to the remote backend using its auth token.

## 4. Watch restart automation
Retain and validate both safeguards:
- `--non-interactive`
- `DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1`

For manager-triggered rebuilds on watch sessions:
1. Prefer sending the watch restart command.
2. Fall back to stop/start using the stored template if interactive restart cannot be issued.

## 5. Agent-optimized log reduction
Keep raw logs in the ring buffer and persisted ndjson files. Add a presentation-layer reducer for tool responses.

Reducer rules:
1. Keep all errors, failures, exceptions, and warnings from the application itself.
2. Summarize compiler and NuGet warning floods.
3. Summarize repetitive framework `info:` chatter, especially `System.Net.Http.HttpClient`.
4. Collapse repeated restore/build output that does not change diagnostic understanding.
5. Keep watch lifecycle events and final build/test outcome lines.
6. Attach filter metadata so clients can see that reduction occurred.

## 6. Measurement support
Use real raw log samples and reduced outputs to calculate:
- raw lines/chars/tokens
- reduced lines/chars/tokens
- savings ratio
- estimated build/start cycles per large-context agent session

## 7. MCP self-build isolation
The runtime persistence work keeps target applications alive across stdio proxy re-instancing, but the MCP server binary itself still locks its default `bin\Debug` output while live backend daemons are loaded.

Operational rule:
1. Build or test `CanDoItAll.Mcp.DotNetWatch` itself with `--artifacts-path` when live backends are running.
2. Only fall back to stopping the live backend daemons when a default-output build is explicitly required.

This prevents validation and developer workflows from fighting the persistent backend ownership model.
