# Subbundle 08 — Observability, Proof, and Release Gate

## Goal

Make this work auditable and safe to accept.

## Implementation tasks

1. Add/verify telemetry fields.

Required trace/log tags:

- `agentframework.structured_output_contract_key`
- `agentframework.structured_output_raw_hash`
- `agentframework.finalizer_mode`
- `agentframework.finalizer_status`
- `agentframework.finalizer_invocation_count`
- `agentframework.finalizer_raw_hash`
- `agentframework.repair_attempt_count`
- `agentframework.repair_original_raw_hash`
- `agentframework.repair_final_raw_hash`
- `agentframework.tool_policy_decision`
- `agentframework.tool_policy_signature`
- `agentframework.tool_approval_effective`
- `agentframework.provider_supports_structured_output`
- `agentframework.provider_supports_tool_approval`

2. Add command-proof artifact.

Codex must create or update a markdown file:

```text
docs/agent-runtime-hardening-verification.md
```

It must include exact command outputs or summaries with timestamps:

```bash
dotnet --info
dotnet restore <solution>
dotnet build <solution> --configuration Release --no-restore
dotnet test <solution> --configuration Release --no-build
```

3. Add static regression checks.

Add test or script that flags:

- `structuredOutput: null` in approval continuation paths.
- `MetadataJson: "{}"` for governed process-step runs unless finalizer mode is supplied elsewhere.
- `return JSON` prompt-only patterns for machine-critical agents.
- workflow decisions parsed from markdown.

4. Documentation.

Update:

- `docs/agent-output-contracts.md`
- `docs/maf-runtime-stabilization.md` or equivalent.

Required doc sections:

- structured output contract lifecycle
- finalizer modes
- repair/retry
- provider capability matrix
- tool approval semantics
- process automation safety invariants

## Acceptance gate

A reviewer must be able to inspect the docs and test output and know which safety gates passed, which were skipped, and why.
