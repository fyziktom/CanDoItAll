# Run Preview Simulation Repair Input

## Raw Request

- The workflow Run Preview start dialog still needs a more generic system for skipping steps during simulation and simulating skipped-step results.
- Skipped project-structure writes currently lose the output shape that downstream nodes expect.
- Plugin implementations should be able to provide simulation output shape proposals.
- Built-in major nodes should define simulation shapes outside code where practical, ideally JSON or YAML files.

## Observed Failure

- Route: `http://localhost:5107/agents/workflows`
- RunId: `6ad0cbe3-94bc-41eb-9657-4e740f39b98b`
- WorkflowId: `18c1a1da-ccb9-4201-93f5-e3a554399908`
- VersionId: `23053e40-f284-401f-840f-90e3e659510b`
- BackendRunId: `6ad0cbe3-94bc-41eb-9657-4e740f39b98b`
- CreatedAt: `2026-05-14 11:08:13`
- UpdatedAt: `2026-05-14 11:08:49`

The skipped project-structure write step caused `mark-gmail-processed` to fail because `gmail.mark-message-processed` expected `$.inputPayload.runContext.gmailProcessing.messageIds[0]`, but the skipped step did not provide a shaped result envelope.

## Repair Requirement

- Replace the project-structure-only skip switch with typed per-step preview simulation.
- Preserve downstream payload shape when a step is simulated.
- Keep simulation selection scoped to preview execution and do not mutate saved workflow definitions.
- Let plugin executors publish simulation descriptors through the executor catalog.
- Load built-in project-structure simulation templates from external JSON instead of hardcoding those envelopes in UI code.
