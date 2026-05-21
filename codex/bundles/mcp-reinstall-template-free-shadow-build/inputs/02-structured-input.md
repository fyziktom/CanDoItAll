# Structured Input

## Core Objective

- Repair MCP reinstallation so it builds MCP outputs without copying repository templates into MCP artifact paths, while preserving the existing MCP setup and skill sync behavior.

## Success Criteria

- The full `tools\Reinstall-CanDoItAllMcps.ps1` script completes.
- DotNetWatch shadow preparation builds standard repo Release output first, then copies final output files into `.artifacts\mcp-server-shadow`.
- MCP artifacts do not contain copied top-level `Templates` directories.
- Repo-managed skills still sync through the reinstall script.

## Hard Constraints

- Do not move or delete `Templates`.
- Do not remove skill synchronization.
- Do not run DotNetWatch directly from repo `bin`; keep a copied shadow artifact for launch.

## Allowed Side Effects

- Script changes in the MCP reinstall and DotNetWatch bootstrap paths.
- MSBuild target/property changes needed to make repository template copying opt-out for MCP builds.
- New bundle proof files under this bundle.

## Source Artifacts

- `inputs/00-original-request.md`
- `C:\repositories\CanDoItAll\Directory.Build.targets`
- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1`
- `C:\repositories\CanDoItAll\tools\CanDoItAll.Mcp.DotNetWatch\Start-CanDoItAllDotNetWatchMcp.ps1`

## Input Coverage Signals

| ID | Raw note | Owning subbundle |
| --- | --- | --- |
| NOTE-001 | Moving agent templates into `Templates` was correct. | SB01 |
| NOTE-002 | MCP server installation should not need those templates. | SB01 |
| NOTE-003 | MCP reinstall must build MCPs, setup them, and setup also skills as it does. | SB01 |
| NOTE-004 | Shorten the hash may help but is not full solution. | SB01 |
| NOTE-005 | MCP-related projects do not have strong dependency that would load `Templates`. | SB01 |
| NOTE-006 | Build standard Release in repo and copy final MCP build outputs into artifacts. | SB01 |
| NOTE-007 | Validate that it is working. | SB01 |

## Dependency And Sequencing Signals

- SB01 is the only subbundle and must close all notes.
- Prepared-stage validation must pass before implementation.
- Full reinstall proof must pass before final bundle closure.

## Validation Expectations

- Capture current-state/failing-first evidence before the fix.
- Capture a full passing reinstall transcript after the fix.
- Scan MCP artifacts to prove copied repository templates are absent.
- Assert source changes preserve skill sync and artifact manifest behavior.

## Evidence Contract

- `proof/SB01/transcripts/failing-first-current-state.txt`
- `proof/SB01/transcripts/reinstall-pass.txt`
- `proof/SB01/transcripts/artifact-scan.txt`
- `proof/SB01/transcripts/changed-file-hashes.txt`
- `proof/SB01/transcripts/anti-stub-audit.txt`
- `proof/SB01/source-assertions.md`
- `proof/SB01/verifier.md`
- `proof/SB01/manifest.md`

## UI Validation Strategy

- N/A - no UI surface is changed.

## Browser Validation Analytics

- N/A - host/build-script proof replaces browser proof for this bundle.

## Working Assumptions

- Repository template copying remains useful outside MCP install, so it should stay default-on and be disabled by MCP callers.
- The MCP output directories contain everything needed to launch after a normal Release build.

## Primary Risks

- A copied shadow artifact could omit runtime support files.
- A publish command could still copy templates if the opt-out is not applied consistently.
- A targeted build could pass while full reinstall still fails.
