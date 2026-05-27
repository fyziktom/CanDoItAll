# SB02: 02-maf16-feature-adoption-matrix

## Goal

Create a concrete adoption matrix for MAF 1.6 features.

## Required work

- List all relevant MAF 1.6 features from official sources.
- For each feature mark: Adopt now, Defer, Not applicable, Blocked.
- At minimum cover IChatMessageInjector, AgentSessionFiles/file store, stream error input persistence, tool approval/middleware, local/hosted MCP metadata, workflow evaluation expected output, A2A v1, OpenTelemetry auto-wiring, skills/frontmatter.
- Link each adopted feature to code/tests.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB02` are updated and downstream subbundles can rely on the behavior.
