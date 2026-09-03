# C# Boundary Ownership

| Responsibility | Owner | Allowed dependency / input | Forbidden leakage |
|---|---|---|---|
| Safe outcome/error/effect values | Models; Runtime.Abstractions only for runtime port envelopes | Existing typed side-effect mode, bounded identifiers, enums | SDK objects, HttpClient, UI component types, raw exception/argument payloads |
| Argument-shape validation and AIFunction result conversion | Maf Runtime top-level adapter | AIFunction/JsonElement and generated schema; produces neutral values | ProjectStructure-specific switches; provider-specific success policy |
| Invocation outcome normalization | Maf typed boundary adapters with neutral output | Supported domain/workspace/MCP result contracts | Reflection probing as success authority; receipts accepted from arbitrary model JSON |
| Terminal mutation assessment and recovery policy | Core Execution top-level policy | Trusted traces/receipts and current invocation context | ProviderKind checks, tool-name suffix tests, UI rendering, storage layout |
| Bounded canonical tool-evidence projection | Core Execution/Chat | Current authorized session/run/source and persisted safe outcomes | Old serialized provider session/approval reuse; cross-project or cross-agent evidence |
| Protocol normalization and relay | Maf Runtime/Providers; SharedProviders.Http | SDK/client messages and advertised capabilities | Project graph mutation and run completion decisions |
| Asset commit/readback identity | Workbench ProjectStructure service and focused tool adapter | Existing managed storage and authorization services | Agent-written path accepted as managed authority; direct filesystem node registration |
| Durable receipt publication | Existing Core persistence path and Web response mapping | Safe typed fields; legacy Unknown | RequestSummary, tokens, protected exception text exposed verbatim |
| Context refresh routing | Core context contract / Modules.AgentFramework orchestrator/hub | Trusted committed effects with existing source identity | Inferring commit from assistant prose or refreshing every project |
| Canvas rendering | Workbench page/context provider | Matching notification and canonical reload | Tool execution policy or provider conditions in Razor |
| DI wiring | Existing composition/module registrations | Narrow constructors for touched collaborators | Service-locator access, broad shared context bags, circular references |

## Minimal extraction plan

SB01 extracts the touched tool-call feedback boundary from MafRuntimeAgentFactory and delegates normalization to cohesive adapters. SB02 adds a small terminal outcome policy consumed by both existing completion branches. SB03 adds a bounded evidence projector. SB05 separates asset operation orchestration and telemetry from the already large nested builder only as needed for correct commit evidence. Do not move methods into partial files and call that separation.

Before adding any abstraction, list its production producer and consumer. Prefer a concrete policy or delegate for a single implementation; use an interface only for a real dependency boundary or independently substituted test seam.
