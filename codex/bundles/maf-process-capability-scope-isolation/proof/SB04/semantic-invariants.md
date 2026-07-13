# SB04 Semantic Invariants

## Invariant MAF-SB04-RUNTIME-HANDOFF

- Invariant ID: `MAF-SB04-RUNTIME-HANDOFF`
- Source raw note: process-specific restrictions and instruction fragments must reach the agent runtime without polluting common MAF defaults.
- Expected behavior: process scope directives translate to `AgentRuntimeCapabilityScopeOverride`, trusted metadata fails closed when malformed, runtime context intent receives the override, and process-scoped instruction fragments are appended by the process brief.
- Disallowed shallow implementation: adding process text to global MAF prompts or referencing the MAF wrapper from process assemblies.
- Failing-first test: `bundle://proof/SB04/transcripts/adversarial-negative.txt` proves there is no process-side dependency on `CanDoItAll.AgentFramework.Maf`.
- Passing test: `Process_execution_metadata_carries_scoped_capability_policy_to_runtime_intent` in `bundle://proof/SB04/transcripts/passing.txt`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessCapabilityScopeTranslator.cs` with hash `B61481DA80712722BB958CC036C9F06E896D6FAE9F561910931FC1A5CC58311E`.
- Production assertions: translator mappings are explicit and malformed trusted scope metadata creates a closed deny-all override.
- Red-team negative case: a malformed governed-process scope payload must not silently fall back to unrestricted tools.
- Downstream dependency check: SB05 process templates use the translator path to scope development image guidance to the right step only.
