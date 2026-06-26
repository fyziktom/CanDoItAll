# SB03 Proof Manifest

## Summary

- Subbundle: `SB03`
- Status: `Completed`
- Owned requirements: `R009`, `R010`
- Owned raw notes: `N004`, `N009`
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`

## Changed File Manifest

| Path | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs` | `modified` | `4BB05D1575BD8008CDB4BBDDB2DCB9B29A8E1F9E4680F43C0CE4B61CC6683FE0` |

## Proof Artifact Hashes

| Path | SHA-256 |
| --- | --- |
| `bundle://proof/SB03/transcripts/failing-first-project-structure-proof.txt` | `D0107285735E8244F81A656EC2F2B358EDDBB3640FF652462657C0369EA22042` |
| `bundle://proof/SB03/transcripts/project-structure-comfyui-image-asset.txt` | `6C095C5059F155D342B0DE22A3A5F19D3C2DBC0D17C257237E7B00382E4C59C0` |
| `bundle://proof/SB03/transcripts/asset-content-readback.txt` | `1C8946445CFD18DE90ADEC56A4DF2C589817B94F4DFBD02DEA2487F25BBCA264` |
| `bundle://proof/SB03/transcripts/anti-stub-audit.txt` | `734F2010A871777F738AA0FDB97F95497DF4E24A95729C7188CBAD28D9326081` |
| `bundle://proof/SB03/generated/project-structure-live-comfyui-flux-summary.json` | `07BDA559E783C24EDB57C9B45C96AB9263A8634AA208ACA7B19B0F695485905A` |
| `bundle://proof/SB03/generated/project-structure-live-comfyui-flux.png` | `30AA6AF773ABF9E83054001FD0FADB9AC7CB60DFE6098FBF893C5CA26123D553` |

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB03/transcripts/failing-first-project-structure-proof.txt`
- Passing live proof transcript: `bundle://proof/SB03/transcripts/project-structure-comfyui-image-asset.txt`
- Content readback transcript: `bundle://proof/SB03/transcripts/asset-content-readback.txt`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`

## Source-Level Assertions

- `bundle://proof/SB03/source-assertions.md`

## Semantic Adequacy

- Shipped behavior: a live integration test generated a PNG through the seeded ComfyUI Flux provider and stored it as a project-structure `ImageAsset`.
- Source proof: `bundle://proof/SB03/source-assertions.md`.
- Test proof: `bundle://proof/SB03/transcripts/project-structure-comfyui-image-asset.txt`.
- Content proof: `bundle://proof/SB03/transcripts/asset-content-readback.txt`.
- Shallow-pass trap: manual fixture upload is rejected by the invariant because the proof records the provider, model, project id, asset node id, readback length, and matching SHA-256.
- Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`.

## Browser Or Host Proof

- Host/service proof: `bundle://proof/SB03/transcripts/project-structure-comfyui-image-asset.txt`
- Generated image artifact: `bundle://proof/SB03/generated/project-structure-live-comfyui-flux.png`
- Proof summary: `bundle://proof/SB03/generated/project-structure-live-comfyui-flux-summary.json`
