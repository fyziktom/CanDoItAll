# MCP reinstall build pipeline and proof

## Status

- `Completed`

## Objective

- Repair the MCP reinstall build pipeline so it skips repository template copying for MCP builds, prepares DotNetWatch by building standard Release output and copying the final output to a shadow artifact, preserves existing MCP setup and skill sync behavior, and records host proof.

## Success Criteria

- `tools\Reinstall-CanDoItAllMcps.ps1` completes successfully.
- DotNetWatch shadow manifest points to a copied artifact under `.artifacts\mcp-server-shadow`.
- MCP install/shadow artifacts do not contain copied top-level `Templates` directories.
- Repo-managed skills sync behavior remains present in the script and install manifest.

## Covered Inputs

- NOTE-001 through NOTE-007 from `inputs/02-structured-input.md`.
- REQ-001 through REQ-005 from `requirements/01-normalized-requirements.md`.

## Prerequisites

- Prepared-stage bundle validation passes.
- Source references below exist in the repo.

## Exact Source References

- `C:\repositories\CanDoItAll\Directory.Build.targets`
- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1`
- `C:\repositories\CanDoItAll\tools\CanDoItAll.Mcp.DotNetWatch\Start-CanDoItAllDotNetWatchMcp.ps1`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\CanDoItAll.Mcp.DotNetWatch.csproj`
- `C:\repositories\CanDoItAll\CanDoItAll.Mcp.DotNetWatch.settings.json`

## Deliverables

- MSBuild property gate for repository template copying.
- DotNetWatch wrapper standard-build-plus-copy shadow preparation.
- MCP reinstall publish calls that opt out of repository template copying.
- Proof artifacts under `proof/SB01`.

## Dependency Impact

- This is the only implementation phase. Weak proof would leave the full MCP setup path untrusted and could hide regressions in DotNetWatch shadow preparation, MCP install artifacts, or skill sync.

## Validation Depth

- Process-critical closure. Requires Semantic Adequacy Gate proof, host command transcripts, artifact scan, source assertions, changed-file hashes, anti-stub audit, and a final verifier artifact.

## Implementation Steps

1. Add a controllable MSBuild property around `CopyRepositoryTemplates`.
2. Update DotNetWatch shadow preparation to build in the repo standard Release output with template copying disabled, then copy final output files into a short shadow artifact folder.
3. Update MCP reinstall publish commands to disable repository template copying.
4. Validate with the full reinstall script and artifact scans.
5. Update proof manifest, execution report, raw note closure, and bundle status.

## Scope Exceptions

- None.

## Do Not Do

- Do not move or delete `Templates`.
- Do not remove skill sync or existing reinstall configuration updates.
- Do not launch DotNetWatch directly from repo `bin`.
- Do not treat a hash-shortening-only change as sufficient.

## Acceptance Checklist

- [x] Prepared entry gate passed.
- [x] Source changes implement standard-build-plus-copy for DotNetWatch shadow preparation.
- [x] MCP builds/publishes opt out of repository template copying.
- [x] Full reinstall script passes.
- [x] Artifact scan proves no copied `Templates` under MCP install/shadow outputs.
- [x] Proof manifest references existing transcripts and source assertion artifacts.
- [x] Raw notes NOTE-001 through NOTE-007 are closed.

## Proof Required

- `proof/SB01/transcripts/failing-first-current-state.txt` with the current failing symptom or current-state proof.
- `proof/SB01/transcripts/reinstall-pass.txt` from `powershell -NoProfile -ExecutionPolicy Bypass -File tools\Reinstall-CanDoItAllMcps.ps1`.
- `proof/SB01/transcripts/artifact-scan.txt` proving MCP artifacts do not include copied top-level `Templates`.
- `proof/SB01/source-assertions.md` with source-level assertions for the MSBuild property gate, standard build plus copy, and skill sync preservation.
- `proof/SB01/transcripts/anti-stub-audit.txt` scanning changed production files for `TODO`, `NotImplemented`, and fixture-only shortcuts.
- `proof/SB01/transcripts/changed-file-hashes.txt`.
- `proof/SB01/verifier.md` with final fake-proof resistance review.
- `proof/SB01/manifest.md` tying all artifacts together.

## Browser Validation Logging

- N/A - no browser-visible surface. Host-visible proof is captured through PowerShell transcripts listed in `Proof Required`.

## Progression Gate

- Closure passes only when full reinstall succeeds, artifact scans reject copied templates in MCP outputs, the install manifest is present, skills sync remains in the script/manifest, and `proof/SB01/manifest.md` cites existing transcripts for Semantic Adequacy Gate evidence.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
