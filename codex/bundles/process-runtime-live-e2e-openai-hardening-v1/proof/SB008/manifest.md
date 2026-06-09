# SB008 Proof Manifest

## Status
Completed.

## Objective
Large-desktop browser proof for the global `/processes` launch flow.

## Proof Artifacts
| Artifact | SHA256 |
| --- | --- |
| `bundle://proof/SB008/large-desktop-process-launch-proof.md` | `1C8989F1A2944240FEFFE9E3A36AA25DCC4D4F558A70A326EC70813E32FB714C` |
| `bundle://proof/SB008/transcripts/large-desktop-process-launch-playwright.txt` | `F697704B5BB1CFE4CC01A4680CD2FD7AE0B07601C5D91AF47472678937F07B5E` |
| `bundle://proof/SB008/test-results/SB008-large-desktop-process-launch.trx` | `913DBBEC412E09E74290BAA5E167EBDD746C81ABB43F383AF3F9E6E29CA6FAEE` |
| `bundle://proof/SB008/transcripts/large-desktop-process-launch-source-assertions.txt` | `35424FD06255E1B1898A73407574717D0874AED108B90BC7D353FEE116E0F8CA` |
| `bundle://proof/SB008/transcripts/anti-stub-and-runtime-host-drift-scan.txt` | `E3A68EF94FDC9DF25150C3A247C7D4316FBE0BCA6FF8C3BC5C6EAF967CFE3E40` |
| `bundle://proof/SB008/transcripts/no-transient-bundle-path-scan.txt` | `54338F9C4FA5200FB211644857228C6875AB58581E59DD9C4DF44CE02EF8CFAF` |
| `bundle://proof/SB008/transcripts/no-unexpected-ui-media-drift-scan.txt` | `E3507C62F87FD2CDF747F3EE3626A78039BBAD00917A95D5FADDC209FA0E24B2` |
| `bundle://proof/SB008/screenshots/01-template-selected-large-desktop.png` | `3A5A8988402AFFA6E12F208ED18E1458D2B20DE700522D27A5FBBC8A861D61B1` |
| `bundle://proof/SB008/screenshots/02-runs-tab-before-launch-large-desktop.png` | `14C86784CC0E06A25B7AA12CD27E76215A2D3AFBF48478407894C356541E3790` |
| `bundle://proof/SB008/screenshots/02-launch-plan-created-large-desktop.png` | `B6D3CD5CF0ED3A8DD38BEF3F15E22DCF03FD5D800C6FD42E17A35F03A8697AA2` |
| `bundle://proof/SB008/screenshots/03-run-selected-large-desktop.png` | `0D9E63A204E6028E227F6B7B659DF8EA4A2B5484F469FC06B9CA7FD76BD66172` |

## Result
Passed. The global `/processes` large-desktop process launch proof created a ready launch, executed it into a process run, and captured the required screenshots.

## Boundary
No production code was changed. The launch execution remains routed through the existing process launch service and normal run-start path; no generic process-driver runtime host was added.
