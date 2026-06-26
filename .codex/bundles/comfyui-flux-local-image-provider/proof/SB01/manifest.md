# SB01 Proof Manifest

## Summary

- Subbundle: `SB01`
- Status: `Completed`
- Owned requirements: `R002`, `R003`, `R004`
- Owned raw notes: `N002`, `N003`, `N004`, `N005`, `N010`
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed File Manifest

No production source files changed in SB01.

| Path | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `bundle://inputs/sample/ImageGenerationFlux.json` | `c6e602122424bae24bffd91ccfbee7b8ac6e0d661e60b09fda80956b5a7cff1d` | `c6e602122424bae24bffd91ccfbee7b8ac6e0d661e60b09fda80956b5a7cff1d` |
| `bundle://inputs/sample/ComfyUiService.cs` | `9947242d46c93645165d4077a0f83d4fff34662ba2de1523e1c4927e5a452f46` | `9947242d46c93645165d4077a0f83d4fff34662ba2de1523e1c4927e5a452f46` |
| `bundle://proof/SB01/transcripts/comfyui-flux-live-generation.txt` | `new` | `8079d0e483ba00249e2d5f362373f5b33ecdac1ab334ce474e753f78be11e838` |
| `bundle://proof/SB01/generated/flux-live-image.png` | `new` | `05a9211ace06587593bb37e2f81e9339736f7067eb3d0cb08fee94ffbc457a72` |

## Command Transcripts

- Passing transcript: `bundle://proof/SB01/transcripts/comfyui-flux-live-generation.txt`
- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-missing-flux-node.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`

## Source-Level Assertions

- `bundle://proof/SB01/source-assertions.md`

## Semantic Adequacy

- Raw note owned: `N004`, `N005`, and `N010`.
- Shipped behavior: ComfyUI 0.25.0 accepted the Flux workflow, completed prompt `e39b85c8-abb7-4a66-9859-b45ff4b5cc47`, and downloaded non-empty image bytes.
- Source proof: `bundle://proof/SB01/source-assertions.md`.
- Test proof: `bundle://proof/SB01/transcripts/comfyui-flux-live-generation.txt`.
- Shallow-pass trap: health-only proof or stale generated files would not prove Flux generation.
- Adversarial negative proof: `bundle://proof/SB01/transcripts/failing-first-missing-flux-node.txt`.
- Semantic positive proof: `bundle://proof/SB01/transcripts/comfyui-flux-live-generation.txt` and `bundle://proof/SB01/generated/flux-live-image.png`.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt` states no stale sample generated file, fixture-specific branch, `TODO`, or `NotImplemented` path was used.

## Browser Or Host Proof

- Host/API proof: `bundle://proof/SB01/transcripts/comfyui-flux-live-generation.txt`
- Generated image artifact: `bundle://proof/SB01/generated/flux-live-image.png`

## Downstream Smoke

- SB02 may proceed because SB01 proved the external ComfyUI Flux prerequisite and did not require production code edits.
