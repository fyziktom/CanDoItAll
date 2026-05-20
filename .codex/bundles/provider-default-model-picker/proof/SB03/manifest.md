# SB03 Proof Manifest

## Subbundle

- Id: `SB03-explicit-model-override-canonicity`
- Status: `Completed`
- Owned requirements: `R004`, `R007`
- Raw notes: "saved ... then the dialog get back to unselected" from `inputs/03-follow-up-runtime-model-override-reset.md`

## Changed File Hashes

| Path | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `.codex/bundles/provider-default-model-picker/README.md` | `1a1d15eaac497d60c46db9a5b6eaa12b8b90ada3847e052affcba39afba19596` | `009ca2997d7aa50652bc408dbc10a842d6cb7e0401dfe42022c8bc029237ec5a` |
| `.codex/bundles/provider-default-model-picker/inputs/03-follow-up-runtime-model-override-reset.md` | `N/A (new file)` | `01d3c209b0b859a333eadcd865f0f0467f66ae02be3d4e9acc4e81d902fa9834` |
| `.codex/bundles/provider-default-model-picker/requirements/01-normalized-requirements.md` | `959bb8eb3c652ed4e63c770bd2a4c2e27eb72b466254a88dbab65c1f76cd11ff` | `1d755be429c9f43da4a33c922b5f3cb4933a23d7c4181d434251e8df96eab1e5` |
| `.codex/bundles/provider-default-model-picker/plan/01-phase-plan.md` | `be7c5783231a4fe72471752dc7e2afb932d6333d22102c95edc40b053005dc86` | `cee626c1b0f2dad3d95c48b41c90f7a4d9087b1dcde0970d0f29245fd777f2a0` |
| `.codex/bundles/provider-default-model-picker/traceability/01-requirement-traceability.md` | `f30b8c8df0f35fba24e520ad85ab039e2883fdea0544bc189aea970f746487b4` | `958e4a4243da4717baa6d5d9d340c8c92d8597da45cf388294ac07dd1a875179` |
| `.codex/bundles/provider-default-model-picker/reviews/01-execution-report.md` | `d3eaa98684fea738d30eadf2ff51af4a3e0bb4e0788124191350da72fbcee033` | `b1191b16846077266a5c70106bfb54529917a7a14f21e3f5cffa56533d48a078` |
| `.codex/bundles/provider-default-model-picker/subbundles/03-explicit-model-override-canonicity/README.md` | `N/A (new file)` | `677ce323664b33c680bc70512d073524ae3185b461db9827c4feccb1ca2521e3` |
| `src/CanDoItAll.AgentFramework.Components/ProviderModelSelector.razor` | `754b3b098ede0cea29d95f0a7be6f39e76c1f1d1cb53ca64d0ceb36cf5f318bb` | `9660385c17e4fd246c515ec9ec598cd0ce6bda1a1dcf3d99491b053452710f9d` |
| `src/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.Agents.cs` | `e73195147186ec4290672363a9d6cd79b6a93acec87184c31cdbdf7d10faed83` | `1391ca3863febb7801bb7ea69ff8880c4252eecdb0b2bb5f6cdb98e45ca0de2d` |
| `tests/CanDoItAll.Tests.Components/ProviderModelSelectorTests.cs` | `a91dfe758f8331e7546e0e9d5b58dab4f6365051cc3a756adab3369d96906b99` | `f08e09f1702a20fceb127a97db625a2cbe28543d68a4c666567c0f2da9b31568` |
| `tests/CanDoItAll.Tests.Components/AiAgentsPageTests.cs` | `13316e9593a3c0ffd9c22db3132d521c3081a6e21a2b87b894b3449214e6d4f3` | `d00831dd8f7071007a09128fe33ad231542438cbdc64825ee2038b44beb2d3e2` |

## Command Transcripts

- Build transcript: `proof/SB03/transcripts/passing-build.txt`
- Failing-first transcript: `proof/SB03/transcripts/failing-first-explicit-override.txt`
- Passing transcript: `proof/SB03/transcripts/passing-targeted-tests.txt`
- Source assertion transcript: `proof/SB03/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `proof/SB03/transcripts/anti-stub-audit.txt`
- Browser blocker transcript: `proof/SB03/transcripts/browser-proof-blocker.txt`

## Failing-First Proof

- Transcript: `proof/SB03/transcripts/failing-first-explicit-override.txt`
- Expected failure: selector had no custom text field for non-empty provider-default value and agent dialog saved empty model after explicit override.
- Test name: `ProviderModelSelector_treats_non_empty_provider_default_value_as_explicit_override`
- Test name: `AgentDetails_runtime_model_override_with_provider_default_text_reopens_checked`

## Passing Semantic Positive Proof

- Transcript: `proof/SB03/transcripts/passing-targeted-tests.txt`
- Passing behavior: explicit override with `gpt-5-mini` stores `gpt-5-mini` and reopens with the override text field present, while provider-default dropdown selection still stores empty and reopens unchecked.
- Test name: `ProviderModelSelector_treats_non_empty_provider_default_value_as_explicit_override`
- Test name: `AgentDetails_runtime_model_override_with_provider_default_text_reopens_checked`
- Test name: `AgentDetails_runtime_provider_default_model_saves_as_provider_linked_empty_model`
- Test name: `AgentDetails_runtime_unchecking_model_override_saves_provider_default_and_reopens_unchecked`

## Source Assertions

- Transcript: `proof/SB03/transcripts/source-assertions.txt`
- Assertion: `src/CanDoItAll.AgentFramework.Components/ProviderModelSelector.razor` treats empty as provider default in agent mode, but does not collapse a non-empty provider-default value when `UseEmptyValueForProviderDefault` is true.
- Assertion: `src/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.Agents.cs` normalizes model persistence by trimming only; it no longer compares with provider default during save.

## Anti-Stub Audit

- Transcript: `proof/SB03/transcripts/anti-stub-audit.txt`
- Result: no production TODO, NotImplemented, fixture-specific branching, or test-name branching markers in touched production files.

## Browser Proof

- Blocker transcript: `proof/SB03/transcripts/browser-proof-blocker.txt`
- Result: browser proof is blocked by managed app `HealthTimeout`; app stayed in `Building` for about five minutes with no runtime PID. The timed-out proof session was stopped.

## Red-Team Verifier

- Artifact: `proof/SB03/verifier.md`
