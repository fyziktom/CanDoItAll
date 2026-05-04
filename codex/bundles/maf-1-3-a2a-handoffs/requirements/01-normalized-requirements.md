# Normalized Requirements

## Functional Requirements

- `REQ-01`: Upgrade `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, and `Microsoft.Agents.AI.Workflows` package references to `1.3.0`; add A2A packages only where needed and isolate preview dependencies.
- `REQ-02`: Change OpenAI default model constants, managed seed providers, provider adapters, UI defaults, and tests from `gpt-5-mini` to `gpt-5.4-mini`.
- `REQ-03`: Add typed A2A configuration for remote agent endpoints, discovery mode, protocol binding preference, auth reference, skill exposure, and enabled/disabled state.
- `REQ-04`: Support wrapping configured remote A2A agents as `AIAgent` instances and, where useful, expose their agent-card skills as function tools with sanitized tool names.
- `REQ-05`: Add optional ASP.NET Core A2A hosting for selected CanDoItAll agents with explicit agent cards, endpoint paths, capabilities, and no implicit public exposure.
- `REQ-06`: Add typed handoff orchestration support around MAF `HandoffWorkflowBuilder` so configured agent groups can hand work to other agents and return to the previous agent when enabled.
- `REQ-07`: Integrate handoff mode into process automation so software-delivery/process steps can route implementation, QA, security, architecture, and release work through explicit agent relationships.
- `REQ-08`: Ensure process steps require durable, QA-consumable artifacts and evidence before downstream QA/review steps proceed.
- `REQ-09`: Introduce or repair tool availability profiles for software development, QA, architecture review, security review, business analysis, and strategy roles.
- `REQ-10`: Audit context, session serialization, MAF compaction, transcript replay, and process prompt injection so governed process runs do not lose necessary upstream artifacts or tool results.
- `REQ-11`: Add architecture review subbundles after the initial runtime/model work and after process-flow integration, with authority to insert refactor/remediation subbundles before continuing.
- `REQ-12`: Provide focused unit/integration tests and, for visible UI changes, browser validation proof.
- `REQ-13`: Ensure effective workspace tool access is single-source-of-truth for tool attachment and runtime enforcement, including trusted governed process overrides and catalog `workspace-plugin` tools.

## Non-Functional Requirements

- `NFR-01`: Keep A2A and handoff abstractions strongly typed and owned by the agent framework models/core layers.
- `NFR-02`: Preserve least privilege. New cooperation features must respect `AgentPermissionsPolicy`, workspace tool access settings, provider feature matrices, and existing approval policy.
- `NFR-03`: Make errors diagnosable with actionable logs that identify agent id/name, provider id/name, cooperation mode, endpoint identity, protocol binding, run id, and masked auth state.
- `NFR-07`: Do not expose workspace tools that the same runtime plugin will deny for the current effective profile; denied host tools are a configuration/projection bug, not a normal agent recovery path.
- `NFR-04`: Avoid large refactors unless an architecture review gate proves the current layering blocks safe implementation.
- `NFR-05`: Keep package updates reproducible and validated with `dotnet restore`, targeted builds, and targeted tests before broader solution tests.
- `NFR-06`: Do not change historical EF migrations only to rewrite seed model strings unless runtime seed normalization or tests require it.
