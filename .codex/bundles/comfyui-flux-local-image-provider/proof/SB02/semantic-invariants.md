# SB02 Semantic Invariants

## Invariant SB02-FLUX-PROVIDER

- Invariant ID: `SB02-FLUX-PROVIDER`
- Source raw notes: `N001`, `N005`, `N006`, `N007`, and `N008`.
- Expected behavior: CanDoItAll contains an enabled local ComfyUI Flux image provider whose configuration uses the provided Flux workflow shape, mutates prompt/seed/size/output nodes explicitly, and fails predictably for invalid configured nodes.
- Disallowed shallow implementation: only proving ComfyUI health, adding a provider without the Flux workflow, treating ComfyUI as a chat/tool provider, or falling back to another image provider.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-flux-provider.txt` records that the pre-change source had no local Flux provider/defaults and no configured output-node validation.
- Passing tests: `bundle://proof/SB02/transcripts/passing-focused-tests.txt`, `bundle://proof/SB02/transcripts/comfyui-driver-focused-tests.txt`, and `bundle://proof/SB02/transcripts/comfyui-flux-seed-integration-test.txt`.
- Changed source files: hashes are recorded in `bundle://proof/SB02/manifest.md`.
- Production assertions: Flux defaults live in the model layer, the existing driver stays responsible for ComfyUI HTTP protocol details, the seed layer adds the provider profile, and project-structure code remains outside this subbundle.
- Red-team negative case: `ComfyUiProviderDriver_RejectsMissingConfiguredOutputNodeBeforeEnqueue` proves invalid output node configuration fails before the ComfyUI prompt endpoint is called.
- Downstream dependency check: SB03 may proceed because the runtime catalog can resolve an enabled `ProviderKind.ComfyUi` image-generation provider.
