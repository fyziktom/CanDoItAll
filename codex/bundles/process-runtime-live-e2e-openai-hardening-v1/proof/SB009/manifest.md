# SB009 Proof Manifest

## Status
Completed.

## Objective
Critical Gate C proves the global `/processes` UI started a real current process run through launch-plan execution and verified it through API/service readback, not a seeded baseline.

## Changed Files
SB009 made no production source changes and no long-lived test source changes. It added only proof artifacts under `bundle://proof/SB009` and updated bundle execution documentation.

## Proof Artifacts
| Artifact | SHA256 |
| --- | --- |
| `bundle://proof/SB009/semantic-invariants.md` | `72F77614E26E39598668F3BFE25B2B032159F20866E04DC837A854FD66D3A113` |
| `bundle://proof/SB009/transcripts/web-build-no-restore.txt` | `BC410750EB6A71E56331356B8F958819291D292EC5B75268A773930202AA0F39` |
| `bundle://proof/SB009/transcripts/global-ui-real-run-playwright.txt` | `73E7641A6B81528CA3530617784A2AEDA89EC297048EDAEC2EF3A4E66B428914` |
| `bundle://proof/SB009/test-results/SB009-global-ui-real-run.trx` | `9B1857C2E6C7143AB52AA72675C52F612E97C1A55D8172CAF80A9E6A4A1607DF` |
| `bundle://proof/SB009/transcripts/global-ui-real-run-source-assertions.txt` | `87CB4FA521B16D5F8B7D6E9656C8BF2D849BBEFB563424C08CA0A2C7C9782EBC` |
| `bundle://proof/SB009/transcripts/red-team-seeded-baseline-rejection.txt` | `96A29BEAEBA9A46BC3755583B6C9398F15153549836CF31AD6D27F82BFF31CD4` |
| `bundle://proof/SB009/red-team/seeded-baseline-only-proof.txt` | `324A39030592601C052873610615CF8D023574D18383673849BB874121E9DC31` |
| `bundle://proof/SB009/transcripts/anti-stub-and-runtime-host-drift-scan.txt` | `E3A68EF94FDC9DF25150C3A247C7D4316FBE0BCA6FF8C3BC5C6EAF967CFE3E40` |
| `bundle://proof/SB009/transcripts/no-transient-bundle-path-scan.txt` | `54338F9C4FA5200FB211644857228C6875AB58581E59DD9C4DF44CE02EF8CFAF` |
| `bundle://proof/SB009/transcripts/no-unexpected-ui-media-drift-scan.txt` | `E3507C62F87FD2CDF747F3EE3626A78039BBAD00917A95D5FADDC209FA0E24B2` |
| `bundle://proof/SB009/screenshots/01-template-selected-large-desktop.png` | `EDC81C79303D48D8D9AD0B29D969DC7CA39C99BCC3C39DA6799B93B85305458B` |
| `bundle://proof/SB009/screenshots/02-runs-tab-before-launch-large-desktop.png` | `F6DC51DB093C916E21A05FB3879442DE7FA40951F3D200CA65F2E08EA3017C87` |
| `bundle://proof/SB009/screenshots/02-launch-plan-created-large-desktop.png` | `830ED5CD6A4930D704B190132FF2CF274E643C6A79CD8B7DA4640EE2D60C55A0` |
| `bundle://proof/SB009/screenshots/03-run-selected-large-desktop.png` | `12415238541C15716E37F4D2FB77959494D86FA926CABF9BC3FA55B0DA6B04BE` |

## Result
Passed. Gate C confirms the global UI launch proof uses a freshly created UI-driven run with API readback and selected-run summary validation.

## Boundary
No runtime-host, driver-host, registry, selector, manager command, scheduler hook, workflow hook, or Process Core orchestration changes were introduced.
