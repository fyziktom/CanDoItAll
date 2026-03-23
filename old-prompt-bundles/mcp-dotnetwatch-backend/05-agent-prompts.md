# Agent Prompts

Use these prompts as execution guards while implementing. Do not jump phases without passing the validation bullets inside each prompt.

## Prompt 1: Backend bootstrap

"Implement dual startup modes in `CanDoItAll.Mcp.DotNetWatch`: stdio MCP proxy mode and backend web-daemon mode. Add backend registry and bootstrap logic so multiple stdio startups reuse one compatible backend process. Do not move tool logic yet. Validate by proving the backend health endpoint can be reused across two separate stdio process launches without spawning duplicate daemons."

Validation:

1. backend registry file is written
2. second stdio startup reuses backend
3. stale registry replacement works

## Prompt 2: Proxy the MCP tools

"Move runtime ownership to the backend. The stdio MCP process must call backend HTTP endpoints instead of using in-process runtime state. Preserve existing MCP tool names. Validate by starting an app through one stdio process, disposing it, then reading status and logs through a second stdio process."

Validation:

1. same app session ID visible across two stdio instances
2. same watcher/runtime PIDs visible across two stdio instances
3. logs remain readable after re-instancing

## Prompt 3: Multi-session runtime manager

"Refactor app-session ownership so the backend can run more than one live app session. Reuse compatible sessions, detect conflicts, and keep backward-compatible status behavior for no-argument calls. Validate with at least one reuse scenario and one conflict or multi-session scenario."

Validation:

1. compatible start reuses
2. different project or non-conflicting launch can coexist, or conflict rules are enforced explicitly and tested
3. workspace info surfaces multiple sessions or a deterministic default plus a session list

## Prompt 4: Operation preemption hardening

"Update build/test operation handling to reason about multiple live sessions. Only preempt conflicting sessions, capture resume outcomes, and bias defaults toward keeping watched apps alive. Validate with tests showing unrelated sessions stay up and conflicting sessions resume cleanly."

Validation:

1. unrelated session is not stopped
2. conflicting session stop/resume behavior is explicit
3. payload identifies affected sessions

## Prompt 5: Backend manager UI

"Add a lightweight backend dashboard using `CanDoItAll.Manager` as a template. Show backend identity, live sessions, operations, and links/logs. Keep it diagnostic-first. Validate by opening the page and verifying that a reused session is visible there after MCP re-instancing."

Validation:

1. dashboard reachable on loopback
2. dashboard shows the live reused session
3. dashboard shows backend identity and recent operations

## Prompt 6: Project structure page live validation

"Fix the lower section of `ProjectStructurePage` so the outline and graph-health area have a better layout and large outlines scroll instead of growing the entire page indefinitely. Validate it through a live backend-owned watch session, then re-instance the MCP server and confirm the page stays alive and receives a style-only change through watch."

Validation:

1. page layout improved
2. lower section scroll behavior fixed
3. backend-owned app survives MCP re-instancing
4. style change after re-instancing appears in browser without starting a new app session
