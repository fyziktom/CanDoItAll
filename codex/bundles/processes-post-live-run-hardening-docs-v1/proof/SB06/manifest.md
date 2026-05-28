# SB06 Proof Manifest

## Status

Completed.

## Goal

Harden project-structure process run folder projection with an explicit Workbench policy that exposes useful current-run folders while rejecting noisy artifact subtree nodes.

## Changed Files

| File | Purpose | Hash proof |
| --- | --- | --- |
| repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessRunFolderProjectionPolicy.cs | New typed policy for current-run managed roots, artifact roots, product output roots, and ignored noisy paths. | bundle://proof/SB06/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAssemblyService.cs | Routes process-run artifact folder grouping through the explicit policy and classifies source hints by projection kind. | bundle://proof/SB06/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.Modules.Workbench/README.md | Documents Workbench projection ownership, managed output roots, and raw external-target alias boundaries. | bundle://proof/SB06/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.Modules.Processes/README.md | Updates the Processes architecture map to point run folder projection at the Workbench policy. | bundle://proof/SB06/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs | Adds policy matrix coverage and expands the Workbench surface test with current-run receipt collapse and wrong-run rejection. | bundle://proof/SB06/transcripts/changed-file-hashes.txt |

## Failing-first Or Adversarial Proof

- bundle://proof/SB06/transcripts/failing-first.txt records a non-zero search for the removed `ResolveManagedOutputDirectoryPath` helper and an adversarial policy test that rejects wrong-run, dated receipt, and traversal paths.

## Passing Proof

- bundle://proof/SB06/transcripts/passing.txt records 2 passing targeted integration tests: the direct policy matrix and the end-to-end project-structure folder projection surface test.

## Source Assertions

- bundle://proof/SB06/transcripts/source-assertions.txt records the dedicated policy, contributor call site, source-hint classification, negative test inputs, and README ownership docs.

## Anti-stub Audit

- bundle://proof/SB06/transcripts/anti-stub-audit.txt records no TODO, pending, stub, or `NotImplementedException` markers in the SB06 changed runtime, test, and README files.

## Changed-file Hashes

- SHA-256 `878DA61A61B78273951B4FF69ECAEAF77200D7153F2CB60B8EB3228534D5F3E5` repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessRunFolderProjectionPolicy.cs
- SHA-256 `D4F9678590A4B27DA6661A1F295665D2160BCF73DC498F1CC5D987474CBA72E9` repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAssemblyService.cs
- SHA-256 `7CB30B9721211EE9FEDA371B55F95969673E4BB79BFFFCEEEFA7CF01814D9A91` repo://src/CanDoItAll.Modules.Workbench/README.md
- SHA-256 `96F0EB3890CA110969CE6BC7029947CE1BBEAB28013ACBA845575D928E934AAA` repo://src/CanDoItAll.Modules.Processes/README.md
- SHA-256 `75A76EC19FA66F9FBBEC06FF97BFCB083A3E5FC650A856A959DE4186C9FD2AFA` repo://tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs
- bundle://proof/SB06/transcripts/changed-file-hashes.txt records the command transcript for these hashes.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| Typed run folder projection policy | repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessRunFolderProjectionPolicy.cs via `Resolve`; source proof bundle://proof/SB06/transcripts/source-assertions.txt | repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAssemblyService.cs process projection contributor | Normalizes managed artifact paths, selects current-run artifact root or product output root, and returns a typed projection kind before node grouping | Wrong-run, dated receipt, absolute, traversal, and unanchored paths return `Ignored`; adversarial proof bundle://proof/SB06/transcripts/failing-first.txt |
| Project-structure run output folder nodes | repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAssemblyService.cs grouping by policy directory and projection kind | Workbench project-structure surface and downstream SB09/SB13 navigation assumptions | Adds one folder node per useful current-run root with managed storage reference and source hint; end-to-end proof bundle://proof/SB06/transcripts/passing.txt | Current-run receipt internals collapse into the run artifact node and unrelated run outputs do not appear; proof bundle://proof/SB06/transcripts/passing.txt |
| Projection ownership documentation | repo://src/CanDoItAll.Modules.Workbench/README.md and repo://src/CanDoItAll.Modules.Processes/README.md | Downstream docs/templates/observability subbundles SB09, SB12, SB13, and SB18 | Documents that raw `external-target/...` aliases remain Processes grounding metadata while Workbench projects persisted managed output roots | Anti-stub and source assertion proof prevents docs-only or placeholder closure; bundle://proof/SB06/transcripts/anti-stub-audit.txt |

## Browser Validation

N/A. SB06 changed projection policy, grouping, tests, and README documentation. It did not change project-structure Razor markup, CSS, route wiring, canvas layout, or visible UI rendering components.

## Closure

- SB06-INV-001 is satisfied by repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessRunFolderProjectionPolicy.cs and bundle://proof/SB06/transcripts/passing.txt.
- Noisy-folder negative proof is satisfied by bundle://proof/SB06/transcripts/failing-first.txt.
- The expanded end-to-end surface test is recorded in bundle://proof/SB06/transcripts/passing.txt.
- SB09 and SB13 may rely on current-run folder projection after this gate.
