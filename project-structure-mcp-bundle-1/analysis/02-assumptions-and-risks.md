# Assumptions And Risks

## Working Assumptions

- The main CanDoItAll machine can expose HTTP endpoints to the local network or at least to trusted workstations.
- Agent identity can be represented as a persisted profile plus token without integrating a heavier auth system in this first pass.
- The existing workspace settings page can absorb an additional agent-policy tab without requiring a larger navigation redesign.
- The workbench node model already covers most needed project-structure objects, so the new MCP can rely on existing node types instead of inventing new primitives.
- Setup material can be generated from persisted settings such as a configured base URL and agent token.

## Critical Path Risks

- If the central API contract is too broad or poorly typed, the MCP server will become a thin transport shell around unstable JSON blobs.
- If lease semantics are too coarse, agents will block each other unnecessarily; if too fine, the same repo or node can still be mutated concurrently.
- If approval rules are added only in the MCP client and not the central API, another client or machine can bypass them.
- If import support is over-scoped early, the foundation subbundle may stall on file-format edge cases instead of delivering the central access model.

## Validation Risks

- A tool-only happy path could pass while the settings UI is unusable or unclear on real screens.
- Unit tests alone will not prove that remote MCP instances can actually create and read project-structure content through the central HTTP layer.
- Asset flows are easy to under-test because read-only retrieval and revisioned replacement are different behaviors.
- Setup scripts can look correct but still fail on another machine if install outputs, config paths, or generated instructions are not exercised.

## Reopen Triggers

- Reopen subbundle `01` if later MCP validation reveals missing API fields, incorrect checklist propagation, or weak locking conflict details.
- Reopen subbundle `02` if the generated setup instructions or policy UI are confusing in the browser or do not reflect the actual MCP settings schema.
- Reopen subbundle `03` if the MCP client needs to hardcode knowledge of central policy or layout fields that should belong in shared contracts.
- Reopen the bundle if final validation cannot prove delivery-block plus document-asset creation and readback through the real MCP flow.
