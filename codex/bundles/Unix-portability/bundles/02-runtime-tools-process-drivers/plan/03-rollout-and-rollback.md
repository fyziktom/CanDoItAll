# Runtime rollout and rollback

## Rollout principles

- Keep runtime features capability-gated and disable-able.
- Introduce shared execution primitives before migrating callers.
- Migrate one owner/caller family at a time: Workbench, Manager, MCP/tools, plugins, Processes.
- Retain bounded compatibility for legacy runtime-node metadata.
- Do not remove old process discovery/launch behavior until new ownership evidence is green.
- External dependency support is opt-in per proven profile/version.

## Recommended stages

1. B00 characterization with no implementation.
2. B01 process primitive behind internal adapters; run old and new only in non-side-effecting characterization, never dual side effects.
3. Workbench direct execution cutover with terminal/elevation disabled on Unix/macOS.
4. Manager launched-process registry; keep old discovery read-only until proof.
5. MCP/external tool cutover.
6. Docker/FileTools capability cutover.
7. Process strategy capability preflight.
8. Actual-host CI and canary profiles.
9. Final support matrix and R4.

## Rollback

Each phase must support reverting to the previous binary/config while preserving:

- runtime-node metadata and compatibility reader;
- process registry/journal;
- no orphaned processes;
- old MCP/package tool root;
- plugin configuration;
- capability/support profile;
- Core C4 data/key state.

Before rollback, stop/identify owned processes using the new registry and preserve evidence. Never fall back to name-only termination.

## Rollback blockers

Invoke B90/B91 when:

- process ownership is ambiguous;
- residual children cannot be identified safely;
- an external dependency changed persistent state incompatibly;
- a runtime metadata migration is destructive;
- the old/new execution path would both perform side effects;
- process-domain/MAF ownership is no longer clear.
