# 38 Context Transport And Adapter Modularization

## Status

- `Completed`

## Execution Outcome

- HTTP and MCP transport code is decomposed into non-partial configuration, request, invocation, and response collaborators with strict header/environment binding and typed failures.
- Remote MCP uses the official SDK transport, scoped DI, explicit tool mapping, timeouts, and only supported query/status behavior; unsupported ingestion/source/feedback/event behavior is rejected rather than advertised.
- Provider-management UI was decomposed into focused components/services with lossless tags/limits/unknown-extension preservation and credential-reference migration; raw credentials block save.
- A hosted background worker cycles operation, feedback, inbox/outbox, and retention phases with per-phase isolation. SB40 added PostgreSQL atomic owner/token-fenced phase leases with renewal/expiry; InMemory coordination remains explicitly process-local.
- Reported focused evidence: AgentFramework.Mcp and Memory.Mcp builds 0 warnings/errors; MCP runtime contracts 27/27; Memory MCP driver tests 12/12; worker-hosting tests 17/17; provider-management UI modularization tests 26/26 with owned production files at or below 219 lines; Memory.Http build 0 warnings/errors after HTTP012-HTTP014 hardening.
- SB40 completed the real-host desktop/narrow provider-editor and truthful unsupported-action proof.

## Objective

- Propagate typed workspace/execution/project identity from the active agent run to every memory request, make HTTP/MCP provider configuration lossless and secret-safe, register supported transports in production, and modularize transport drivers without capability-grouping partial classes.

## Success Criteria

- Agent, runtime session, workflow/process, workspace, and project identity are carried in typed request context from MAF to the generic operation handler and HTTP/MCP envelopes.
- No production memory path depends on magic tag names for execution identity.
- Editing a provider preserves selection tags, unknown extension data, transport endpoint/tool mapping, and secret references.
- API keys/tokens are resolved through an explicit secret reference at dispatch and are never persisted or rendered as plaintext.
- HTTP and MCP are registered through production composition only when configured, and missing/invalid configuration fails with typed diagnostics.
- Driver files are cohesive non-partial types with separate request factories, response mappers, and transport invokers.
- Advertised capabilities match implemented driver behavior; unsupported status/events/feedback/ingestion operations fail before remote dispatch.

## Covered Inputs

- R01
- R03
- R04
- R07
- R12
- R13
- R16
- R20
- R26
- R27
- R28

## Prerequisites

- SB37 completed with typed agent bindings, runtime routing, and multi-provider orchestration gates passing.

## Exact Source References

- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/02-csharp-dependency-direction.md`
- `bundle://architecture/03-csharp-pattern-selection-records.md`
- `bundle://architecture/04-csharp-testability-plan.md`
- `repo://src/Memory/CanDoItAll.Memory.Abstractions/MemoryProtocolContexts.cs`
- `repo://src/Memory/CanDoItAll.Memory.Abstractions/MemoryRequests.cs`
- `repo://src/Memory/CanDoItAll.Memory.Abstractions/MemoryProviderRegistryContracts.cs`
- `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderConfiguration.cs`
- `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.cs`
- `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderRequestFactory.cs`
- `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderResponseMapper.cs`
- `repo://src/Memory/CanDoItAll.Memory.Http/NativeRemoteMemoryProviderDriver.cs`
- `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderConfiguration.cs`
- `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderDriver.cs`
- `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderRequestFactory.cs`
- `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderResponseMapper.cs`
- `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderManagementUiService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderEditorModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.Memory/Pages/MemoryProvidersPage.razor`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.RuntimeToolProviders.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentContextContributionProvider.cs`
- `repo://src/App/CanDoItAll.Web/Program.cs`
- `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryHttpDriverTests.cs`
- `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryMcpDriverTests.cs`
- `repo://tests/Memory/CanDoItAll.Memory.Tests/NativeRemoteMemoryProviderDriverTests.cs`
- `repo://tests/Components/CanDoItAll.Tests.Components/MemoryProvidersPageTests.cs`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/MemoryProviderManagementPlaywrightTests.cs`

## Deliverables

- Extend the generic query/request context with strongly typed workspace, execution, policy, and budget data needed by external providers, using optional init-only properties or a compatible contract revision rather than positional-constructor churn.
- Populate agent ID/role, runtime session, workflow/run/node, process/run/step, workspace kind/key, project ID/name, correlation, and caller identity from actual MAF runtime intent; delete reliance on `memory.workflowId`, `memory.processId`, and related tag keys.
- Carry the same typed context through context contributor, runtime tool, workflow executor, operation request builder, ledger, HTTP request factory, MCP request factory, and native-remote wrapper.
- Introduce typed HTTP and MCP provider configuration codecs/builders that preserve unrelated `MemoryExtensionData`, selection tags, default policy, workspace scope, UI surface metadata, and unknown future fields during edit/save.
- Store only a strongly typed secret reference in provider configuration and resolve it through an injected secret resolver at dispatch; mask endpoint credentials, headers, tokens, request content, and sensitive extension values in logs and errors.
- Add driver-kind-specific provider editor sections using the existing BaseLib components for endpoint, manifest/tool mapping, timeout, secret reference, and capability validation; never display resolved secret material.
- Split the broad provider-management UI service into cohesive profile query/command, configuration mapping, operation projection, and surface-resolution collaborators without moving UI concerns into transports.
- Register HTTP, MCP, native-remote, and mock driver factories from host composition with explicit configuration predicates and startup validation; no transport is an implicit fallback.
- Split HTTP and MCP partial drivers into non-partial transport invokers, request factories, response mappers, and driver facades with narrow dependencies and namespaces.
- Convert configuration, serialization, timeout, cancellation, protocol, and remote errors into typed operation failures with actionable non-sensitive logs; do not catch and continue with another provider.
- Validate provider manifests at registration/refresh and expose only implemented operations; reject a request for an unadvertised operation locally with a typed capability mismatch.

## Dependency Impact

- SB39 relies on accurate project/caller context and secret-backed authentication reaching the Cognitive Memory service.
- SB40 relies on lossless provider editing and production MCP/HTTP composition for real E2E scenarios.
- Any context defaulting or extension loss here can create cross-project disclosure or silently break external providers.

## Validation Depth

- `Critical transport, configuration, and context boundary`

## C# Architecture Impact

- Protocol context gains explicit identity fields while retaining provider neutrality.
- HTTP/MCP drivers become thin adapters over dedicated request, invocation, and response collaborators.
- Provider management UI maps typed driver configuration and preserves the rest of the profile instead of rebuilding a lossy subset.
- Secret resolution becomes an infrastructure boundary invoked only at transport dispatch.

## Boundary Ownership

- Memory Abstractions owns provider-neutral workspace/execution/policy/budget contracts and extension preservation semantics.
- Agent Framework Memory owns deriving those contracts from a live agent run.
- HTTP/MCP projects own driver-specific options, protocol mapping, invocation, and sanitized error translation.
- Module Memory owns provider editor UI; the host owns secret storage/resolution and conditional transport registration.

## Dependency Direction

- `AgentFramework.Memory -> Memory.Abstractions/Application` supplies context; transports must not reference Agent Framework types.
- `Memory.Http/Mcp -> Memory.Application/Abstractions` is allowed; neither transport may reference module UI, host composition, Persistence, or native Cognitive Memory domain types.
- Module UI may reference typed provider configuration contracts but transport projects must not reference the module.
- Secret implementations depend on a host/security abstraction; Memory Abstractions must not depend on a concrete secret store.

## Pattern Decision

- Use Adapter plus dedicated request/response mappers for each transport.
- Use typed configuration codecs/builders with lossless extension merging; do not model transport settings as ad hoc dictionaries in UI code.
- Use a secret-resolver port because credential storage is an external infrastructure boundary with multiple implementations.
- Use explicit exception-to-result translation at the driver boundary; do not add retry/fallback chains that hide configuration or provider errors.

## Testability Contract

- Request factories and response mappers are pure or dependency-light and directly testable with full context fixtures.
- Transport invokers accept injectable `HttpClient`/MCP client and secret resolver; tests can assert headers/tool payloads without real credentials.
- Profile editor round-trip tests start with unknown extensions and selection tags, edit one field, persist/reload, and compare all untouched data.
- Composition tests resolve every configured driver and fail startup for missing endpoint/tool map/secret reference.

## Partial Class Policy

- Delete `HttpMemoryProviderDriver.Requests.cs`, `.Responses.cs`, `McpMemoryProviderDriver.Requests.cs`, and `.Responses.cs` as partial fragments; retained files must declare named independent top-level types.
- Driver facade, request factory, response mapper, configuration codec, secret resolver, and invoker are non-partial.
- Razor code-behind and generated serialization/regex code remain allowed.

## Architecture Proof Required

- Show transport project dependency graphs before/after and prove no Agent Framework, module, host, Persistence, or native-domain reference leaks into adapters.
- Run source audits for magic memory identity tags, raw API key/token persistence, secret values in rendered markup/logging, and prohibited transport partial declarations.
- Add architecture tests for lossless profile round-trip, production MCP registration, and transport-only dependency direction.
- Record API compatibility impact for each protocol contract change and confirm external conformance ownership in SB39.

## Implementation Steps

1. Turn SB35 missing-context, lossy-profile, and unregistered-MCP characterizations into focused red tests.
2. Add compatible typed context contracts and populate them from real MAF runtime intent across all agent memory paths.
3. Refactor HTTP/MCP drivers into facades, request factories, invokers, response mappers, and typed error translation.
4. Add typed lossless provider configuration and secret-reference resolution.
5. Update provider editor components and persistence mapping to preserve all unrelated profile data.
6. Register and validate configured transports in production composition and enforce capability honesty.
7. Run unit, component, Playwright, composition, dependency, partial, secret-leak, and build gates.

## Scope Exceptions

- Native Cognitive Memory endpoint authentication, project authorization, domain access policy, and cross-repository independence are owned by SB39.
- This phase must not invent support for remote operations the provider does not implement; capability removal is preferred to fake success.

## Do Not Do

- Do not default a missing or malformed project to global scope.
- Do not persist API keys or tokens in provider extension JSON, logs, screenshots, or test snapshots.
- Do not discard unknown extensions or selection tags when a provider is edited.
- Do not add another magic-tag convention or transport-specific context type to MAF.
- Do not register MCP/HTTP unconditionally or silently switch transports after failure.

## Acceptance Checklist

- A real agent run produces envelopes with correct agent/session/workflow/process/workspace/project identity through HTTP and MCP.
- Missing required project/workspace identity fails before dispatch where provider policy requires it.
- HTTP/MCP profile edit round-trips all untouched extensions, tags, policy, scope, and UI metadata byte-equivalently or semantically equivalently.
- Resolved secrets appear only in outbound transport headers/credentials and are masked everywhere else.
- Production composition resolves configured HTTP/MCP/native/mock drivers and rejects invalid configuration at startup or explicit operation time.
- HTTP/MCP drivers are non-partial and each collaborator has focused tests.
- Unsupported capability requests return typed mismatch and produce zero remote calls.
- Cancellation and timeout remain distinct observable results.

## Proof Required

- Create `proof/SB38/manifest.md` and `proof/SB38/semantic-invariants.md` with hashes and portable source/transcript/screenshot references.
- Failing-first proof: capture project context dropping, provider extension loss, raw-secret configuration, missing MCP production registration, and overclaimed capability behavior before the repair.
- Positive proof: run real HTTP and MCP test servers through production drivers and assert complete typed context, resolved credential use, response mapping, ledger correlation, and successful profile edit/reload.
- Negative proof: exercise missing project, missing/invalid secret reference, malformed endpoint/tool mapping, timeout, cancellation, invalid response, unsupported capability, and unavailable provider; verify typed failures and zero fallback.
- Anti-stub proof: inspect captured outbound HTTP/MCP requests plus real operation-ledger entries; pure mapper assertions or hand-built response records are insufficient.
- Run focused Memory, Unit, Components, Playwright, composition, secret-leak, dependency, partial, and build gates.

## Browser Validation Logging

- Target the generic Memory Providers page and driver-specific editor surfaces.
- Run maximized desktop and narrow-width passes for HTTP and MCP profiles.
- Use Playwright to edit one transport field on a profile preloaded with tags/unknown extensions, save/reopen, and assert preserved data through the real UI/API path.
- Capture screenshots with only secret-reference labels, never secret values; review validation placement, long endpoint/tool-map wrapping, and narrow layout.

## Progression Gate

- SB39 may start only after typed end-to-end context propagation, lossless profile editing, secret masking, production transport registration, capability honesty, driver modularization, browser proof, and the SB38 architecture checkpoint pass.

## Suggested Agent Prompt

```text
Implement SB38 only. Propagate typed runtime context, harden lossless secret-safe HTTP/MCP configuration, register supported transports, modularize the drivers, and prove real outbound requests and profile round-trips. Stop on any context, secret, capability, or architecture gate failure.
```
