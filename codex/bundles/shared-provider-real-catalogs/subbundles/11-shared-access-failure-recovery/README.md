# SB11: Shared access failure recovery and project image proof

## Status

- Status: Completed
- Validation: Behavioral/architecture proof and final regression review pass;
  unchanged broad-suite failures are recorded in proof/SB11/broad-regression-results.md.
- Proof tier: Governed (credential privacy boundary and actual customer-run recovery).

## Objective

R15/N015, inputs/08-project-image-failure-feedback.md: explain and repair run
bfb2e58e-411f-4766-be91-ea952333bba1, then validate Portfolio Architect creating a
calculator UI image and attaching it to the same project's structure through UI.

## Covered Inputs

- inputs/08-project-image-failure-feedback.md (R15/N015).

## Prerequisites

- SB10 feature proof is intact. The test credentials expired normally, independently
of model capability changes. Entry checkout 3eb4d4af0 was clean and all three hosts
run model-thinking-20260828-2. Exact run usage proves HTTP 401 before tool invocation.
Entry gate: Pass once prepared validator succeeds; no production edits before it.

## Exact Source References

- repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentProviderFailureDisplayFormatter.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Providers/Diagnostics/ProviderFailureDisclosurePolicy.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderTransportBoundaryChatClient.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeResponseAssembler.cs
- repo://tests/Unit/CanDoItAll.Tests.Unit/AgentProviderFailureDisplayFormatterTests.cs
- repo://tests/Unit/CanDoItAll.Tests.Unit/MafProviderTransportBoundaryChatClientTests.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs
- repo://tests/Unit/CanDoItAll.Tests.Unit/ImageGenerationAgentRuntimeToolProviderTests.cs
- repo://src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderRelayRequestPolicy.cs
- repo://tests/Unit/CanDoItAll.Tests.Unit/SharedProviderRelayPolicyTests.cs

## Deliverables and implementation sequence

1. Capture exact failed run/status, expired-token metadata only and source non-admission.
2. Failing-first tests for shared HTTP 401/403 remediation and safe status propagation.
3. Consume existing typed boundary status in the display formatter. Distinguish source
   token rejection, denied permissions and other failures; never infer definite expiry from 401 alone.
   Red transport tests also exposed lost status on HttpRequestException (SDK status was retained).
   Preserve that typed status in the existing MAF transport capture switch; no remote body escapes.
4. Rebuild/deploy with volumes intact. Prove the new actionable failure through UI before renewal.
5. Create a bounded, least-privilege source token and update the client secret through UI.
6. Exercise the exact project chat, approval, image generation and attached image asset;
   correlate source usage and inspect the resulting image. Preserve original failed run.
7. Re-entry after run 349c6e81-8472-4923-9eec-e6b6abcf52a7: authentication succeeded,
   but the agent twice requested unsupported size 1536x864. ImageGenerationCreateInput
   did not describe allowed options and NormalizeOption threw an untyped exception,
   reduced to "Error: Function failed." Add schema descriptions and use the existing
   IAgentToolFailure contract for safe, correctable option errors. Never coerce a size
   silently or disclose arbitrary invalid input. Add red/green real-tool tests and
   repeat the project UI image flow on the final image.
8. Re-entry after run cf11744f-0b2a-4426-a1ac-9a77983da4aa: image creation and
   attachment succeeded, but readback analysis rejected the 1,720,707-byte PNG.
   IsDataImageUrl incorrectly uses the 1 MiB text limit on base64 image data.
   Keep the existing 4 MiB total OpenAI request cap, text limits, MIME/role/capability
   and URL restrictions. Validate base64 without allocating a decoded image. Add
   realistic-size and boundary/negative tests; retry analysis of the existing asset
   through UI without generating another image or modifying project requirements.

## Acceptance Checklist

- A shared 401 identifies the provider and guides the user to its source connection/token;
  403 identifies the permission check. The pure formatter does not add a source-directory lookup.
- Safe numeric status survives nested exceptions. Raw remote bodies, URLs and tokens never escape.
- A local upstream 401 is not mislabeled as shared-source credential failure.
- Tool-origin failures are not falsely classified as provider failures.
- Expired token still fails; no auth bypass, unbounded renewal or silent transport change.
- Real UI run produces a calculator image asset using project context and records source usage.
- All three containers return HTTP 200 Healthy with data preserved; actual credential
  expiry is in the handoff. HTTP health does not imply the separate 5212 token is valid.

## Dependency Impact

- The existing Core display formatter consumes the Providers boundary; the existing MAF
transport capture additionally retains HttpRequestException.StatusCode. No API/schema,
provider routing, dependency, runtime composition, catalog, model policy or image-driver changes
are initially planned. Reopen the scope before editing any additional owner if live proof finds
a second fault. Do not mark image generation solved merely because authentication succeeds.
The confirmed image-option fault also changes the existing module tool's validation result
and adds parameter descriptions to its generated schema; request property types and JSON
names remain unchanged. Existing MAF safe-tool-failure mapping is reused without changes.
The confirmed vision-readback fault also touches the existing SharedProviders.Http
request-policy owner. Only image-data validation changes; the global request budget,
auth, routing, upstream transport and descriptor contracts remain unchanged.

## Validation Depth

- Unit xUnit/VSTest on .NET 10. Stable filters: AgentProviderFailureDisplayFormatterTests,
MafProviderTransportBoundaryChatClientTests, MafWorkflowExecutorFailureDiagnosticsTests,
plus new shared-access formatter cases. Freeze exact --list-tests names before each run;
zero/missing tests fail. Original TRX and transcripts are required. CodeAnalytics actual-diff
impact determines additional Unit selectors; a broad gate runs only once if explicitly required.
No solution-wide gate by habit. Browser same-flow proof covers the rendered consumer and
actual service composition; no Razor component is changed. Invalidation keys: safe status
extraction, auth remediation, source-vs-upstream ownership, credential state, final image.

## Boundary Ownership

Existing Core formatter owns user guidance; Providers owns sanitization; MAF owns transport
capture. Keep those responsibilities. No new interface/project/service locator or runtime partial.
Direct isolated formatter tests and existing boundary tests prove the seam without a live runtime.

## Dependency Direction

CodeAnalytics snap-20260828101839-660712e3 loaded Core (172 documents) and Providers (29),
no blocking diagnostics, 923 dependency edges. Existing same-file nested type cycle in
AgentReferenceDataCache is unrelated; no project cycle or proposed new reference.
Core already references Providers. No boundary extraction or class split is justified.

## Pattern Decision

Retain the existing pure formatter and bounded exception traversal. A typed status check is
sufficient; a new registry, fallback transport or generalized authentication service is not.

## Testability Contract

Use real sanitized boundary producers with hostile diagnostic payloads and varied status codes.
No copied status fixture pretending to be live auth proof. No added partial classes.

## Partial Class Policy

No partial class changes are needed for a pure formatter repair.

## UI composition

1920x1080 desktop only. Existing project canvas, contextual chat and approval overlay remain
the primary surfaces. No new cards, tabs, CSS, or layout. Existing alert wraps actionable text;
chat/dialog body owns overflow. Inspect failure alert, approval actions and resulting asset.
Reuse source Settings/API and client Settings/Secrets dialogs for credential setup.

## Proof Required

- proof/SB11/manifest.md, semantic-invariants.md, exact source/test/bundle hashes, original
red/green test artifacts, safe incident evidence, Playwright UI/host/source usage proof,
anti-stub audit and source-backed final review. Components guidance inspected; no new component
selection or setup is needed. No independent reviewer is claimed.

## Architecture Proof Required

Direct isolated tests, unchanged project references, scoped CodeAnalytics and source review.

## Progression Gate

- Code tests precede deployment. Credential recovery precedes positive image execution.
Close only when the original request is satisfied through its real producer path.
