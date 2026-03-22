# Why The Persistent DotNetWatch Backend Changes Daily AI Development

The `CanDoItAll.Mcp.DotNetWatch` update turned the MCP server from a short-lived command bridge into a persistent runtime control layer. That shift matters because coding agents like Codex and GitHub Copilot do not always keep a single MCP process alive for an entire task. They reconnect, re-instance tools, retry flows, and sometimes behave as if every call starts from zero.

Before this update, that behavior created waste:
- extra MCP instances
- confusion about whether the app was already running
- repeated app starts
- unnecessary rebuilds
- noisy logs that burned context and attention

After this update, the runtime itself stays stable even when the MCP stdio proxy does not.

## What Changed

The new model separates responsibilities:
- the stdio-facing MCP server can come and go
- a detached backend daemon owns the actual runtime state
- `dotnet watch`, app sessions, logs, and operations stay attached to that backend
- the backend can manage multiple different workspaces at the same time

In practice, that means an agent can drop the MCP connection, re-open it later, and still reconnect to the same live application session instead of trying to start everything again.

## Biggest Benefits We Confirmed

### 1. App runtime survives MCP re-instancing
This is the core win. We validated that both `CanDoItAll` and `pveinvoicing` kept running even after the MCP stdio layer was re-instanced. The backend identity stayed the same and the app session stayed usable.

Why that matters:
- the agent does not lose the running app just because its tool session changed
- development work can continue without paying the startup tax again
- the agent can ask for status, logs, or further changes without rebuilding the world first

### 2. One manager can see all live backends
We added a machine-level backend catalog and an aggregate manager page. During validation, the manager correctly showed:
- the `CanDoItAll` backend
- the `pveinvoicing` backend
- each workspace root
- each live session
- manager controls for rebuild, force rebuild, stop, and force stop

This solves the “I know another app is still running, but the manager only shows one” problem.

### 3. Remote control works across backends
The aggregate manager is not just a status dashboard. It can route actions to another live backend. We validated a remote rebuild of the `pveinvoicing` session through the aggregate manager endpoint exposed by the `CanDoItAll` backend.

That means one backend manager can operate as the control surface for multiple active C# workspaces.

### 4. `dotnet watch` becomes agent-friendly
We confirmed the runtime now enforces:
- `dotnet watch --non-interactive`
- `DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1`

That removes a class of agent failure where `dotnet watch` waits for confirmation that no one is there to provide.

For AI-assisted development, this is critical. The backend must prefer unattended continuity over interactive prompts.

### 5. Generic support works outside the main repo
This was not only validated in `CanDoItAll`. We also proved the same persistent behavior against `C:\repositories\pveinvoicing\PVEInvoicing`.

We made a live CSS change, observed it through the running app, then reverted it and confirmed the revert. That matters because it shows the backend is not just “hardcoded to our main app.” It behaves as reusable infrastructure for other C# applications too.

### 6. Per-session shadow artifacts prevent output-lock conflicts in target apps
App sessions now run through per-session artifact roots under `.mcp-state\artifacts\app-sessions\<sessionId>`.

Why that matters:
- a running watched app does not fight with another launch over the same normal `bin\Debug` output
- the backend can keep one app alive while still managing rebuild/restart flows cleanly
- validation against another app becomes much more reliable

This is one of the key reasons the generic `pveinvoicing` validation worked.

### 7. Log output is finally optimized for agents, not just humans
One of the biggest practical discoveries was how much useless volume agents were consuming from console logs. The raw watch output often contained:
- compiler warning floods
- NuGet warning floods
- Entity Framework info noise
- framework HTTP trace chatter
- repeated restore/build boilerplate

We kept the raw logs available, but added an agent-optimized view that suppresses low-value noise while keeping meaningful failures and outcomes.

Measured result from a real `pveinvoicing` session:
- raw payload: about `35,140` estimated input tokens
- agent-optimized payload: about `2,624` estimated input tokens
- reduction: about `92.53%`

That is not a cosmetic cleanup. It changes how many useful iterations an agent can perform before context pressure becomes a problem.

## Why This Speeds Up Real AI Development

The persistent backend does not just make things “cleaner.” It makes the loop faster.

### Less restarting, less waiting
When an app stays alive across tool re-instancing, the agent avoids:
- restarting the application through PowerShell
- waiting for the app to boot again
- re-running health stabilization from zero
- re-discovering ports and runtime state

The work becomes incremental instead of repetitive.

### Better use of `dotnet watch`
The backend keeps the long-lived watch process in place, so small UI and static-asset changes can flow through the existing watch session instead of being handled by fresh manual start scripts over and over.

That means the agent can spend more time changing code and validating results, and less time rebuilding process state it already had.

### Lower context waste
A large part of AI latency is not only runtime execution, but also input packing. If the agent keeps ingesting noisy logs, context compresses earlier and useful state gets squeezed out faster.

With the measured `92.53%` log reduction:
- more development cycles fit into the same context window
- fewer turns are wasted on repeated warning floods
- responses should get faster in long-running sessions because the agent has less irrelevant text to carry

## A Practical Note About Browser Validation

One useful discovery from validation: sometimes the backend and watch session do everything correctly, but the browser still holds a stale stylesheet response.

We saw this during the `pveinvoicing` CSS validation. The running app was serving the updated `app.css`, but the page did not reflect it until the stylesheet URL was cache-busted.

That means future validation guidance should distinguish between:
- backend/watch failure
- browser-side static asset caching

This is an important operational detail for AI agents doing live UI checks.

## Another Useful Discovery: MCP Self-Builds Still Need Shadow Outputs

Target applications now use shadow artifact paths correctly, but the MCP server project itself can still lock its own default `bin\Debug` output when live backend daemons are running.

So for work on the MCP server itself, the practical rule is:
- use `--artifacts-path` for build/test runs while the live backends are active

This is not a failure of the persistent backend design. It is a normal consequence of keeping the backend process alive on Windows while rebuilding the same assembly.

## What This Means For Codex

This update is especially valuable for Codex-style workflows:
- the tool no longer assumes perfect MCP session continuity
- the runtime state is durable enough to survive agent behavior
- logs are shaped for context efficiency
- multi-project development becomes much more realistic
- the backend starts acting like a stable developer-side service instead of a disposable command shim

That is a much better fit for real agentic development.

## TODO: Measure End-to-End Time Savings

We already proved the architectural and context-efficiency benefits, but we should still run one more focused measurement later.

Todo:
- measure end-to-end speed difference between:
  - repeated app start via PowerShell/script per change
  - persistent backend with long-lived `dotnet watch`
- capture timings for:
  - first start
  - small CSS/static-asset change
  - small C# hot-reloadable change
  - rebuild-required change
  - reconnect after MCP re-instance
- compare:
  - total elapsed time
  - number of rebuilds
  - number of process restarts
  - number of manual recovery steps

That measurement will let us quantify the direct productivity gain from staying synchronized with an already-running `dotnet watch` session instead of rebuilding the app through PowerShell every time.
