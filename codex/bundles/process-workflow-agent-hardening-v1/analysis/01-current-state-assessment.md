# Current State Assessment

## Positive Assessment

The current process/workflow direction is valid. The Tetris run is valuable evidence that the system can coordinate:

- external intake through workflow
- project-structure writeback
- process scope and architecture steps
- Blazor implementation
- QA validation and recovery
- security review
- release readiness
- rollout proof
- post-release learning
- artifact persistence

The hardening work must preserve this direction. The right move is not to roll back the architecture. The right move is to make the working path less fragile.

## Architectural Diagnosis

The main architectural smell is not simply file size. The deeper issue is that the platform has multiple overlapping rule systems:

1. **Template rule system**: JSON definitions, sidecar markdown, process scenarios.
2. **Runtime rule system**: dispatch service, tool policy, execution state, finalizers, artifact validation.
3. **Agent rule system**: agent instructions, skills, prompts, MCP/tool descriptors.
4. **UI rule system**: workflow canvas, process editor, provider/capability setup, live dashboards.
5. **Evidence rule system**: artifacts, browser screenshots, runtime command receipts, tool receipts, process graphs.
6. **Test rule system**: integration fixtures, component tests, baseline scenarios.

These rule systems currently overlap but are not fully canonicalized. As the number of processes, workflows, agents, and features grows, drift will become the main source of hard-to-debug failures.

## Refactoring Strategy

Do not start with "split every large file". Start with "extract the stable contracts that those files are enforcing".

Recommended order:

1. Build canonical inventories and contract records.
2. Extract pure policy/evaluation services around existing behavior.
3. Add characterization tests before behavior changes.
4. Move string ids and JSON paths behind canonical descriptors or typed wrappers.
5. Add usage/evidence ledgers where summaries are currently derived from lossy metrics.
6. Refactor UI to consume typed state instead of duplicating interpretation rules.
7. Run real process E2E tests after each critical foundation stabilizes.

## What Must Be Preserved

- Successful Tetris path remains valid.
- Generic software-delivery process remains generic.
- Existing process templates remain importable.
- Existing runtime API contracts must either remain compatible or include explicit migration/compatibility adapters.
- Agents must retain necessary tool access according to operation contracts.
- Browser proof remains required where process steps require it.
- Office365 workflows must not be rerun accidentally during analysis.
- User-visible cost values must become more honest, not just more optimistic.
