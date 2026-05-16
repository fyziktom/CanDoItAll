# Target Solution

## Summary

Keep the existing prerequisite-boundary shape, but harden it so it is safe for high-volume Cognitive Memory ingestion and audited MAF context injection.

## Source Paging And Cursor Target

```text
Snapshot request
  -> typed cursor with source kind, scope, provider/version, last item key, and optional snapshot anchor
  -> provider query ordered by stable source key/time/id
  -> bounded page
  -> manifest with page status and next cursor
```

Target behavior:

- Cursor mismatch is explicit.
- Stale/deleted cursor behavior is explicit.
- Providers do not load all possible items before returning a page.
- Full snapshot hash is not required for every page if computing it forces full materialization.
- If a full snapshot hash is required for a small source, the provider must mark that cost explicitly.

## Redaction And Hash Target

```text
Raw source payload
  -> redacted exposed content
  -> source integrity hash classified as public/internal/restricted
  -> downstream usage policy
```

Target behavior:

- Exposed content is redacted before future embedding or context use.
- Hash classification tells Cognitive Memory whether a hash can be persisted, logged, projected, or displayed.
- Workbench notes and metadata get sensitivity/access metadata rather than unconditional internal/read-only treatment.
- Restricted hashes are never placed into Qdrant payloads or browser-visible trace output.

## MAF Trace Target

```text
IAgentContextContributor
  -> AgentContextContributionResult
  -> MAF context provider
  -> retained contribution trace
  -> future recall/context audit
```

Target behavior:

- Each contributor run has id, status, message count, trace metadata, failure message if any, and elapsed time if practical.
- Trace capture is available to future Cognitive Memory without parsing injected prompt text.
- Context contributor failures remain explicit and do not become silent fallbacks.

## Cognitive Memory Projection

After this bundle closes:

- `02-workbench-and-source-ingestion` consumes hardened source paging/cursors and redaction metadata.
- `05-recall-orchestrator` can trace excluded/skipped/unavailable context channels.
- `07-maf-workflow-integration` consumes contributor trace results rather than relying on private MAF behavior.
