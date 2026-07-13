# SB10 Semantic Invariants

## Completed Validator Contract

- Invariant ID: SB10-CAP-001
- Source raw note: GPTPro finding that deterministic runtime-tool work could be assigned to an incapable prose/profile-only agent.
- Expected behavior: Required runtime tools are matched to assigned typed tool capabilities before provider composition.
- Disallowed shallow implementation: Do not infer tool access from agent role text, profile prose, or markdown instructions.
- Failing-first test: The adversarial negative case is `EvaluateAsync_rejects_workspace_script_when_profile_can_expose_tool_but_agent_lacks_capability` in `proof/SB10/transcripts/01-targeted-unit-tests.txt`.
- Passing test: Positive capability and launch readiness tests pass in `proof/SB10/transcripts/01-targeted-unit-tests.txt`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeToolPreflightService.cs`, `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessLaunchExecutorResolver.cs`, and `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`.
- Production assertions: `CapabilityDiagnostics` are produced by preflight and preserved by runtime issue conversion.
- Red-team negative case: A named `.NET Application Developer` without the typed `workspace-pwsh-run-script` capability is rejected.
- Downstream dependency check: CodeAnalytics snapshot `snap-20260708203629-184e6305` reported no scoped dependency cycles.

## Capability Assignment Rules

- SB10-CAP-001: Required runtime tools are matched to assigned typed tool capabilities before provider composition.
- SB10-CAP-002: Generic agents with prose/profile-only capability are rejected for deterministic tool-plan work.
- SB10-CAP-003: Browser/screenshot required runtime tools require browser/Playwright capability evidence.
- SB10-CAP-004: Template `ExecutionContract.RequiredRuntimeToolNames` flows into launch readiness and launch-plan state.
- SB10-CAP-005: Missing capability diagnostics are separate from missing/composition/scope/args/path/manifest failures.
- SB10-CAP-006: Preflight issue conversion preserves capability diagnostics in runtime issue evidence.

## Anti-Stub Audit

- The capability check is based on assigned typed capabilities and normalized runtime tool names, not agent instruction markdown.
- Existing deterministic tool-plan guard failures still run before capability diagnostics so invalid script refs, invalid managed paths, invalid manifests, and scope/composition denials are not masked.
- The browser/screenshot path requires explicit browser/Playwright capability evidence and does not infer tool access from the template prose.
- `proof/SB10/transcripts/06-anti-stub-audit.txt` found no placeholder implementation markers in changed SB10 files.

## Production Behavior Artifact Matrix

| Runtime signal | Lifecycle invariant |
| --- | --- |
| `CapabilityDiagnostics` | Produced by preflight, consumed by runtime issue conversion, and covered by negative tests that distinguish missing capability from missing composed tool. |
| Template execution-contract required tools | Produced by SB09 template metadata, consumed by SB10 launch readiness and launch-plan state, and covered by resolver tests using the real `dotnet-solution-setup` template. |
| Required browser/runtime tool capability | Required before browser/screenshot runtime tools are accepted; missing Playwright capability reports `McpServer:playwright-local-mcp` instead of a generic tool absence. |

## Architecture

- Capability matching stays in module runtime integration because it depends on assigned agent capability state and provider composition boundaries.
- Template execution-contract data remains owned by template metadata; SB10 only consumes its normalized required runtime tool names.
- No runtime code parses markdown prose to infer deterministic tool capability.
- No new project references were introduced.
- CodeAnalytics snapshot `snap-20260708203629-184e6305` reported no scoped dependency cycles.


