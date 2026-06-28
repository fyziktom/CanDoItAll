# SB07 Parity And Dry-Run Report

## Template Parity

- Default capability template pack materializes the expected catalog keys without duplicates.
- Every template descriptor preserves its stable GUID source through `stableGuidKey`.
- Tool templates preserve runtime tool names, approval defaults, and managed seed version in configuration JSON.
- MCP templates preserve server configuration and allowed tool allowlists.
- The default compatibility policy compiles to typed deny rules for mutation and external action operation classifications.
- Agent template assignments resolve against the template-backed capability catalog.

## Negative Fixture Coverage

- Missing capability template files.
- Duplicate capability keys.
- Invalid runtime tool names.
- Raw HTTP headers.
- Empty local MCP `allowedTools`.
- Invalid policy `defaultEffect`.
- Invalid policy rule `effect`.
- Ambiguous MCP tool selector without `serverKey`.
- Unknown implementation-key selector.
- Missing capability-key selector used by an allow rule.

## Operation Compatibility

- `RunValidation` allows validation/script-execution capabilities and denies mutation, external action, and browser proof candidates.
- `MutateProductTarget` allows write/mutation capabilities and denies validation, external action, and browser proof candidates.
- `CaptureRuntimeProof` allows browser proof capabilities and denies mutation, validation, and external action candidates.
- Coarse agent workspace-tool flags compile to typed runtime-tool deny rules and match current `AgentWorkspaceToolAccessMetadata` behavior for the default tool templates.

## Managed Seed Dry-Run

- Normalizing the seeded document twice preserves capability IDs, kind/key identities, and configuration JSON.
- `workspace-read-file` keeps managed seed version `2026-06-agent-template-teams-v24`.
- `mail-triage-inline-skill` intentionally remains without `managedSeedVersion`, preserving the SB06 compatibility exception.
- No duplicate capability IDs or duplicate `kind:key` identities are created.

## Deferred Compatibility Risks

- Remaining inactive helper methods in `SandboxWorkspaceSeedBuilder.cs` are not active fallback paths. They are deferred to SB11 cleanup to avoid mixing hardening proof with broad source deletion before MAF reconnection.
