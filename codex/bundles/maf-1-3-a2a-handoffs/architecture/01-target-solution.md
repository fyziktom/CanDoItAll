# Target Solution

## Architecture

- Models own typed configuration:
  - A2A remote endpoint definitions, protocol binding preference, auth reference, enabled flag, and skill/tool exposure policy.
  - Handoff graph definitions between local agents and remote A2A agents.
  - Role/tool profiles for software delivery and business analysis agents.
  - Context policy knobs for process-safe compaction and session restore behavior.
- Core owns orchestration contracts:
  - Catalog/execution APIs that expose cooperation configuration without depending on preview A2A SDK concrete types.
  - Runtime execution options for cooperation mode, max handoff depth, tool profile id, required artifact guard, and process correlation.
  - Validation services that can prove handoff artifact availability before downstream steps.
- Maf owns MAF-specific adapters:
  - Package upgrade to MAF 1.3 stable packages.
  - A2A remote agent resolution using `A2ACardResolver`, `AgentCard.AsAIAgent`, and explicit protocol binding options.
  - Optional A2A skill-as-function tools based on agent card skills with sanitized tool names.
  - Handoff workflow construction using `AgentWorkflowBuilder.CreateHandoffBuilderWith(...)` and `.WithHandoffs(...)`.
  - Session/context preservation, continuation tokens, and logging for A2A/handoff runs.
- Hosting owns ASP.NET Core exposure:
  - Configuration-driven A2A server registration and endpoint mapping.
  - Well-known agent card mapping only for explicitly published agents.
  - Auth/endpoint policy integration with existing provider/secret boundaries.
- Modules.Processes owns process semantics:
  - Process templates and dispatch prompts require implementation agents to produce QA-ready artifacts.
  - Runtime progression gates verify artifact records and direct file evidence before downstream QA/review.
  - Handoff flow metadata is used to select cooperating agents and tool profiles.
- Modules.AgentFramework owns UI only:
  - Minimal editor panels for A2A endpoints, handoff relationships, and tool profiles if required by implementation.
  - No business/runtime logic in Razor components.

## Boundaries

- Do not place preview A2A SDK types in long-lived persistence entities or public Core APIs unless wrapped by CanDoItAll model types.
- Do not let remote A2A endpoints receive raw secrets in model JSON. Store references to secret records or configuration keys.
- Do not let handoff tools create recursive same-agent calls without max-depth and correlation guards.
- Do not weaken `ProcessStepOutcomeResult` validation or finalizer policy.
- Do not introduce a new generic orchestration engine. Use MAF workflows for handoff and A2A where the framework already provides the behavior.

## Architecture Review Cadence

- Review gate 1 after subbundles 01-07 verifies package/model/runtime shape before process integration.
- Review gate 2 after subbundle 09 verifies process integration and may add a remediation subbundle before validation.
- Final review closes only after tests and proof show requirements traceability, package upgrade, model defaults, cooperation primitives, tool availability, and context policy.
