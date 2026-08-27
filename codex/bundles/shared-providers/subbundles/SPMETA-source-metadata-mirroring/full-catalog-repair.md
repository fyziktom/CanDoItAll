# Full model-catalog parity repair

State: DONE. Proof tier: Governed. Baseline clean HEAD:
`0ecb6307823576e80f79074187668771b166609a`.

## Raw input and ownership

> It is still not correctly/fully done.
> look at screenshot. it propagate it as just one model. It is not correct. for example openai provider offers bunch of models in shared instance and same it must be on client instance. otherwise people cannot select model for their agents and chats on client instance. repair it.

Screenshot: inputs/full-catalog-feedback.png (original clipboard e86f1b9c).
SPMETA owns this entire finding; older proof remains historical. SB07 remains blocked.

## Analysis and minimal boundary plan

`RuntimeProjection/PersistedProviderProfileMapper.cs` adds the built-in OpenAI chat
model list to persisted suggestions. `Administration/SharedProviderProfilePublicationMetadata.cs`
reads only default and persisted suggestions. The publication eligibility policy
therefore publishes fewer models than the source runtime offers. Previous browser
proof used the publication preview as its own oracle and left the source with a
reduced synthetic catalog after testing removal.

Extract the existing model-list policy into one pure top-level helper in
ProviderManagement, shared by runtime mapping and strict publication metadata reading.
No new project, interface, DI dependency, SDK, runtime partial, or reference edge.
Keep strict validation and the 128-model boundary; default model remains first for
publication. Do not synthesize OpenAI models for Ollama or image providers. Price-only
rows are not permission to advertise an otherwise unavailable model. Preserve opaque
route IDs and explicit missing prices; do not infer rates on the importing client.

## Requirements and proof gates

- FULL-SET: source runtime selectable models equal published and imported models,
  including built-in OpenAI options without manually copying them into suggestions.
- FULL-ISOLATION: Ollama and image lists contain only their source-configured models;
  invalid or over-limit metadata remains rejected; route identity stays stable.
- FULL-UI: independent source Runtime/agent selector oracle, client agent and chat
  selectors, exact available prices/private flag. Configure multiple Ollama models via UI.
- FULL-RUN: select and invoke non-default OpenAI and Ollama models on the client,
  verify central usage model routes; retain image and vision regression coverage.
- FULL-HANDOFF: leave both Docker instances running with expanded catalogs, not the
  temporary removal-test state. Preserve volumes and unrelated 5032/postgres resources.

Dependency path: failing parity regression -> shared policy -> focused unit/integration
and component tests -> rebuilt two-instance UI/run proof -> architecture/semantic review.
Entry gate passes: existing lane authorized; source/test references inspected; no external
credentials needed for deterministic upstream; prior model/price mirroring preserved.
UI composition unchanged: existing provider editor and agent dialog on 1920x1080 desktop,
editor scroll owner; inspect normal prices and open model selector in the dialog.

## Analysis tooling and validation selection

CodeAnalytics pre-edit scoped snapshot `snap-20260827114025-7f10b6cc`: ProviderManagement,
1 project, 69 documents, no blocking errors. This is not a full-solution cycle claim.
Components MCP recommendation failed with Transport closed; reuse existing components.
VSTest/xUnit 2.9.3, SDK 10.0.303 (global 10.0.302 latestPatch). Named changed-boundary
tests only; exact discovery counts and outcomes recorded in new full-catalog transcripts.
No unrelated full-suite or historical SB07 closure is authorized by this repair.

Closure: completed-stage source/hash gate and both full UI runs with independent
runtime checks pass. Reopen if source/client model sets differ, selected non-default route
does not execute, or validation leaves the running source catalog intentionally reduced.

Execution finding: Simple Chats uses its own runtime/Application option DTO and UI
gateway, dropping the runtime model display names and source-managed flag. Extend the
existing option records with these two fields and map them through the existing boundary
to ConversationProviderOption. No new reference edges. Include Simple Chats resolver,
component, and two-instance selector proof; this is within the requested chat selection
scope, not a separate feature. Initial publication/source/snapshot lane: 47 passed.

Impact selection result: `proof/full-catalog-impact.json` returned Low confidence,
incomplete, with all eight production changes unresolved. Each supplied test csproj
was loaded as only one project (referenced production members absent); reflection in
unrelated tests also triggered an AllSuppliedSuites fallback (7,529 source tests).
This is not a trustworthy changed-member selection. Manual reviewed boundary selection
is used instead of treating that workspace-loading limitation as authorization to run
every suite and external lane: publication/source projection/snapshot and Simple Chats
resolver (52), catalog API/sync/runtime composition (52), model/pricing/definition UI (24),
plus the UI-configured source/client browser flow. Full-suite proof is not claimed.

Browser run 1 proved provider metadata parity, then failed because the test read an
agent selector before its asynchronous render (zero options). The helper now waits
for the exact model count before comparing names; no production fallback or retry was
added. New UI runs also emit independent source/client metadata JSON for review.

Browser run 2 exposed a genuine downstream blocker after displaying all 12 models:
AgentDefinitionFactory treated a published shared model as a manual local override and
required a local price row. A source-unpriced model is selectable and callable but
cannot acquire a price on the read-only client. Repair the existing save validation
to check the published model constraint for source-owned profiles, retaining explicit
unavailable pricing and rejecting unpublished selections even if a price row exists.
Local-provider manual pricing rules remain unchanged. Capture failing-first positive
and negative tests, rerun the agent save consumers, rebuild the image and revalidate.

Save-boundary proof: four failing-first cases in full-catalog-unpriced-red-2 cover
published/unpriced, unpublished/priced and missing source constraints (default and
explicit selections). The implementation reuses ProviderModelSelectionPolicy rather
than treating display labels or price rows as authorization. Full-catalog-agent-save-2
passes 39 tests, including local manual-price preservation, external provisioning,
package imports and current-profile workspace mutation consumers. These manually traced
AgentDefinitionFactory callers extend the initial eight-file impact scope. No workflow
save behavior was changed; the intermediate WorkflowCatalogTests selection also passes.

Browser run 3 passed catalog/agent save/chat/image/vision, then encountered the expected
Simple Chats permission denial: Docker host requests are not local-loopback circuits.
The acceptance harness now creates a client token through Settings with only the three
Simple Chats scopes and attaches it to the dedicated client browser. Other HTTP origins
are blocked on that page; the token is redacted before screenshots and never written to
proof. Manual UI check confirms access and all twelve labels. No production authorization
change or anonymous-access bypass was introduced.

The bundled browser still denied the interactive UI with correct scopes. An isolated
probe using installed Chrome 151.0.7922.174 passed both authenticated API access and
the same UI sequence, including two real Simple Chat responses. Final browser tests
therefore use the installed `chrome` channel. This isolates the test-browser difference;
no unverified claim about its low-level WebSocket cause is made. Safe auth proof records
only browser version, scope names and HTTP status, never the token. The focused probe
is full-catalog-simple-chats-probe-2 (1 passed); production image is unchanged.

Final functional proof: 167 focused non-browser tests pass, both complete UI runs pass,
and both production runtime evidence scripts confirm ten complete central successes,
including image generation and four non-default agent/chat selections. Source and client
retain 12 OpenAI chat and 3 Ollama choices after resynchronization. Both final-image
engines are healthy; fixture calls establish routing and persistence, not live vendors.
