# Acceptance evidence — SB13

For each criterion, provide behavioral/source evidence rather than only a test count.

- [ ] The final Release solution build passes at the exact recorded commit.
- [ ] The repository stable filtered test gate passes at the exact recorded commit.
- [ ] Documentation and pending-model-change checks pass.
- [ ] Windows, Linux, and macOS CI jobs pass for the same commit.
- [x] No broad suite was rerun after an unchanged failure merely to seek a different result.
- [x] FINAL explicitly states whether UI/component-isolation work is unlocked.

## Required semantic proof

- Intended case: one `UseLocalCanDoItAllLibraries=false` restore, one Release solution build, one
  stable filtered solution test, the final pending-model check, and one same-commit hosted
  Windows/Linux/macOS matrix prove the immutable candidate.
- Negative/race/crash/failure case: the official NuGet flat-container endpoint returns HTTP 404 for
  `CanDoItAll.FileTools.FileInteraction.Spreadsheet`; version 0.1.18 therefore cannot restore from the
  repository's only configured feed. The sibling source can create that exact package in a disposable
  feed, but doing so would not prove the selected package-source contract or hosted CI.
- Why the old implementation would fail this proof: SB11 already demonstrated that a cold
  nuget.org-only package-mode restore fails `NU1101`; no application-source correction can make an
  unpublished package available to all three hosted runners.
- Exact source owner: package publication belongs to `fyziktom/CanDoItAll.FileTools`; this repository
  owns only the package reference, version pin, configured feed, and final release gate.
- Exact command(s): `git status --short; git rev-parse HEAD`; official NuGet flat-container lookup;
  read-only inspection of `NuGet.Config`, `Directory.Build.props`, the Workbench project, sibling
  source/workflow, and credential names; documentation, bundle, traceability, test-policy,
  architecture, SSE, checksum, JSON, and diff guards.
- Actual result: candidate `dea90cfd4cc77e60f1a7d07a2dc16d44165840f9` was clean and all static
  guards passed. The official package lookup returned HTTP 404. No SB13 restore, solution build,
  stable solution test, pending-model command, or hosted matrix was spent.
- Evidence artifact: `proof/SB13/manifest.md` and its three transcripts.
- Commit SHA: final candidate `dea90cfd4cc77e60f1a7d07a2dc16d44165840f9`; last production
  implementation `58265975e868731e25e39d4bf9109f6010d68127`.

## Blocker and resumption

Publish `CanDoItAll.FileTools.FileInteraction.Spreadsheet` 0.1.18 to nuget.org, or provide an approved
dependency-source/feed correction. Then resume SB13 at a new immutable candidate and execute the still
unused single-shot gate exactly once. FINAL is **Not Ready** and unlocks no dependent work.
