# SB06 changed namespace and public-surface review

State: `PASS` after final independent architecture re-review.

## Boundary placement

| Namespace/layer | SB06 responsibility | Review |
| --- | --- | --- |
| `CanDoItAll.Modules.Workspace` | connector manifest, source-managed ownership, pure graph materialization | correct outer persistence/domain adapter |
| `CanDoItAll.Modules.AgentFramework` | database graph loading, effective-profile mapping, snapshot revision, catalog projection, runtime gateway, voice-provider eligibility guard | correct outer anti-corruption/consumer boundary |
| `CanDoItAll.Composition` | hardened named-client selection and per-request access-context handler | correct concrete infrastructure wiring |
| `CanDoItAll.AgentFramework.Models` | connector-neutral credential/network/feature/model/audio constraints and typed rejection | acceptable inner value contracts with no Workspace identity |
| `CanDoItAll.AgentFramework.Providers` | connector-neutral HTTP selector, source-token disclosure policy, existing OpenAI driver enforcement | acceptable runtime extension seam |
| `CanDoItAll.AgentFramework.Maf` | model enforcement, transport boundary, cancellation/disclosure propagation | existing MAF runtime, no shared connector branch |

No shared source/import entity or SharedProviders HTTP type enters an inner MAF namespace.

## Public surfaces

The following new or materially extended public surfaces are justified by a real cross-assembly or
testable boundary:

- `SharedProviderRuntimeProfileMaterializer`, result, effective profile, and availability enum expose
  the pure Workspace-owned materialization contract to the outer AgentFramework module.
- `ProviderCredentialBinding`, `ProviderNetworkAccessPolicy`, and `ProviderFeatureConstraints` carry
  connector-neutral runtime inputs already required by the outer mapper and inner driver.
- `ProviderModelSelectionConstraint`, `ProviderModelSelectionPolicy`, and
  `ProviderModelSelectionException` enforce exact publication-owned routing models in both raw and
  MAF SDK dispatch. The exception has deterministic public text and typed internal properties.
- `ProviderAudioCapabilityPolicy` and `ProviderAudioCapabilityException` fail source-managed STT/TTS
  before credential/network access with typed operation/provider properties and safe public text.
- `IProviderHttpClientSelector` crosses the inner-provider/Composition boundary. Its only production
  implementation remains internal to Composition; the interface avoids an inner reference to Http
  or Workspace and enables deterministic tests.
- `ProviderFailureOperation`, `ProviderFailureDisclosurePolicy`, and
  `ProviderFailureBoundaryException` provide one connector-neutral disclosure contract across raw,
  MAF, workspace-catalog, activity, and workflow boundaries.
- `SharedProviderConnectorManifestSource` is public because Workspace connector discovery consumes
  it through `IConnectorManifestSource`; it declares no editable schema or per-profile secret.

The concrete selector, access-context handler, and catalog projection observer remain internal.

## Dependency and runtime review

- No project file changed after SB05; the existing 34 direct scoped product references are reused.
- The captured before/after selected-reference transcripts contain 103 rows each and have no delta.
- The 8/8 architecture lane verifies canonical Workspace persistence, no inner outer-layer
  references, no source project-reference cycle, explicit connector registration, outer mapper
  ownership, and UI delegation boundaries.
- The frozen 16/16 runtime-projection lane additionally checks loaded inner assemblies do not
  reference Workspace or SharedProviders implementation assemblies.
- Final CodeAnalytics snapshot `snap-20260825100508-300644c7` reports 14 projects, 766 documents,
  35 modules, 5,281 dependency facts, 34 direct product references, zero project cycles, unchanged
  governed two module/one nested-type cycles, and zero error findings.
- Final independent architecture re-review after the audio repair reports `PASS` with no P1/P2
  blocker. Independent security re-audit also reports `PASS` with no P1/P2 blocker.

## Pattern and testability review

The selected anti-corruption/effective-profile adapter remains intact. Shared ownership is expressed
as metadata and typed constraints around the existing OpenAI-compatible runtime, not a connector
switch inside MAF. The HTTP selector and disclosure policy are minimal interfaces/policies with
multiple real consumers and clear deterministic test seams.

Materialization is pure. HTTP proof uses a deterministic local server. Registry/snapshot/hybrid
proof uses real PostgreSQL fixtures. The named client is reused to detect request-context leakage.
Unavailable/no-fallback behavior is exercised through production reconciliation, registry, and
preparation services rather than self-asserting mocks.

Audio is handled as another connector-neutral capability policy, not a shared connector branch.
Driver proof rejects STT/TTS before secret/network access; the existing voice component proof filters
shared profiles and preserves empty selection for an explicitly ineligible ID without personal
fallback. Personal voice behavior remains unchanged.

## Partial-class review

No partial class was introduced or extended for SB06. The mapper change remains an outer mapping
method; graph materialization, catalog commit observation, HTTP selection, access-context
propagation, and failure disclosure are separate focused types.

## Result

The public surface is the smallest credible connector-neutral seam set for the required cross-layer
behavior. No `ProviderKind.Shared`, duplicate runtime/master, Workspace-to-Http edge, inner reverse
reference, new provider-sharing UI flow, or adjacent provider abstraction was introduced. The only
Razor edit is the existing voice picker eligibility/no-fallback guard.
