# Normalized Requirements

## Requirements

| ID | Requirement | Owning Subbundle | Proof Expectation |
| --- | --- | --- | --- |
| MAF2-R001 | Inventory every remaining `MafAgentRuntime` partial file, private nested builder, private nested DTO, and runtime-owned helper before implementation. | SB01 | Inventory artifact plus architecture guard baseline. |
| MAF2-R002 | Move runtime configuration DTOs and composition records out of `MafAgentRuntime` before extracting builders that depend on them. | SB02 | Top-level internal records/classes with direct parser/normalizer tests. |
| MAF2-R003 | Replace `RuntimeCapabilityComposition` references to nested builders with named top-level capability component contracts. | SB03 | Build plus direct composition coordinator tests. |
| MAF2-R004 | Extract `ContextCapabilityBuilder`, `SkillCapabilityBuilder`, `ToolCapabilityBuilder`, and `McpCapabilityBuilder` into top-level components that do not accept `MafAgentRuntime owner`. | SB04 | Direct unit tests for each builder and guard scan for `new *Builder(this)`. |
| MAF2-R005 | Split MCP responsibilities into local MCP, hosted MCP, secret binding, Playwright launch/cache, schema wrapping, and result compaction collaborators where test boundaries justify it. | SB04 | Positive/negative tests for local/hosted MCP and secret binding. |
| MAF2-R006 | Extract workspace runtime plugin behavior into named drivers/factories instead of one hidden nested plugin with file, command, artifact, image, and policy responsibilities. | SB05 | Direct tests with fake file/command/artifact/image services and host-visible command smoke where applicable. |
| MAF2-R007 | Extract input attachment preparation and session serialization/persistence into named services. | SB05/SB06 | Direct tests for request-scoped attachment filtering, analysis prompt generation, and session serialization decisions. |
| MAF2-R008 | Extract finalizer response recovery, process-artifact recovery, provider-failure diagnostics, and repeated-tool invocation guard from `MafAgentRuntime`. | SB06 | Direct unit tests for success/failure/recovery semantics without constructing full runtime. |
| MAF2-R009 | Keep `MafAgentRuntime` as a thin `IAgentRuntime` adapter that delegates to explicit collaborators and owns minimal request orchestration only. | SB03/SB06/SB08 | Architecture guard: runtime line/member count falls materially and no private nested builders remain. |
| MAF2-R010 | Do not introduce a new god service, service-locator layer, or broad `MafRuntimeManager` that hides the same responsibilities. | All | Self-review, code review, and architecture guard scans. |
| MAF2-R011 | Migrate tests away from private nested/runtime static helpers toward direct collaborator tests. | SB07 | Updated tests plus no reflection against moved runtime internals. |
| MAF2-R012 | Add architecture guard tests that fail on new private nested classes/builders under `MafAgentRuntime` except explicitly allowed small exception/control-flow types. | SB07 | Guard tests with a denylist/allowlist and source scan proof. |
| MAF2-R013 | Measure startup/capability composition impact before and after extraction. | SB08 | Captured metrics/transcripts using `IMafRuntimeCompositionMetrics` or focused timing harness. |
| MAF2-R014 | Preserve behavior parity for existing MAF handoff/runtime flows. | SB08 | Focused unit suite, MAF handoff integration slice, and documented full-suite baseline status. |

## Non-Requirements

- Do not add Financial Strategist, quotation extraction, margin calculation, MarkItDown, or document-specific behavior.
- Do not repair unrelated repository baseline failures unless a MAF extraction directly caused them.
- Do not make broad public API changes unless required by existing project boundaries.
- Do not add XML documentation comments.

## Completion Definition

This bundle is complete only when:

- `MafAgentRuntime` has no private nested capability builders.
- capability composition uses top-level contracts/records.
- tests can construct extracted builders/drivers directly.
- architecture guards prevent regression into new partial/nested runtime classes.
- performance evidence is captured and recorded.
