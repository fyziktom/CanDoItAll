# SB01 Proof Manifest

## Status

- Subbundle: `SB01 - Proof Portability And Semantic Invariant Gates`
- Status: `Completed`
- Owned requirements: `R-01`, `R-02`, `R-16`
- Raw notes: proof portability, semantic invariant enforcement, deterministic validation before production cognitive-memory changes.
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed File Hashes

Complete before/after file hashes are recorded in `bundle://proof/SB01/transcripts/changed-file-hashes.txt`.

Primary after-change SHA-256 values:

- `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py`
  - SHA-256: `f195935db4ee94025416733c3fa944975bae96bace2e945fa7e8f94e11b0fe2b`
- `repo://codex/skills/bundles/candoitall-bundle-preparation/SKILL.md`
  - SHA-256: `f096837d9b0377651f1b3641da10232c80e025c886b4154ae118ede590593e0b`
- `repo://codex/skills/bundles/candoitall-bundle-execution/SKILL.md`
  - SHA-256: `c6a38c1d80c360026970963be181b5c68a3a1d9805832039b6b8217224f6143a`
- `repo://codex/skills/bundles/candoitall-bundle-execution/references/artifact-backed-proof-manifest.md`
  - SHA-256: `53caf566a6d9ca51abde474f896158f4f619e42c570c60d05c70963a18c9c3ef`
- `repo://codex/skills/bundles/candoitall-bundle-workflow/SKILL.md`
  - SHA-256: `a99693646027de32ca91c0dbae95abde4113473bae374c6ded7966ccdf7689d9`
- `repo://codex/skills/bundles/candoitall-subbundle-validator/SKILL.md`
  - SHA-256: `aa0c55da75aa679aa9ac660598f004c0fa4786927adce52b080ee1d7448250ee`
- `repo://codex/skills/bundles/candoitall-bundle-validator/SKILL.md`
  - SHA-256: `03121349c16327d16096d0cdfe76c343311428d57007f07f09c8c1bf49d739a3`
- `bundle://proof/SB01/semantic-invariants.md`
  - SHA-256: `6ab71eb3e38c206fc0e547526323a5b76c0d44e6dd8f5697dad9b7ea90d82f62`

Active Codex skill-root hash comparison is recorded in `bundle://proof/SB01/transcripts/active-skill-sync-hashes.txt`.

## Command Transcripts

- Syntax validation transcript: `bundle://proof/SB01/transcripts/py-compile-validator.txt`
- Failing-first transcript: `bundle://proof/SB01/transcripts/fake-proof-fixtures.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/positive-portable-fixture.txt`
- Source assertions transcript: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Downstream prepared validation transcript: `bundle://proof/SB01/transcripts/prepared-validator-after-sb01.txt`
- Hash transcript: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`
- Active skill sync transcript: `bundle://proof/SB01/transcripts/active-skill-sync-hashes.txt`

## Tests And Invariants

- Test name: `ValidatorScript.Compiles`
- Test name: `ArtifactProof.ValidatesCompleteFixture`
- Test name: `ArtifactProof.ValidatesRelocatedPortableFixture`
- Test name: `ArtifactProof.RejectsMachineSpecificOnlyPaths`
- Test name: `ArtifactProof.RejectsMissingOrUncitedInvariantContracts`
- Test name: `ArtifactProof.RejectsProseOnlyProof`
- Test name: `ArtifactProof.RejectsMissingTranscript`
- Test name: `ArtifactProof.RejectsFakeTestName`
- Test name: `ArtifactProof.RejectsMissingHash`
- Test name: `ArtifactProof.RejectsMissingFailingFirst`
- Test name: `ArtifactProof.SourceAssertionsCoverPortableAndInvariantGates`
- Test name: `ArtifactProof.AntiStubAudit`
- Test name: `ArtifactProof.ActiveSkillCopiesMatchRepository`
- Test name: `Bundle.PreparedValidationAfterPortableReferenceConversion`
- Test name: `ArtifactProof.ChangedFilesHaveSha256Hashes`

Invariant IDs covered by transcripts:

- `SB01-PORTABILITY-01`
- `SB01-INVARIANT-02`

## Source Assertions

`bundle://proof/SB01/transcripts/source-assertions.txt` proves the validator contains `--repo-root`, `--bundle-root`, `repo://`, `bundle://`, `PORTABLE_REFERENCE_PATTERN`, `resolve_reference_path`, semantic invariant contract validation, portable proof-manifest rejection, and invariant ID transcript matching.

## Red-Team Negative Proof

The failing-first transcript rejects:

- `artifact-proof-machine-specific-paths`
- `artifact-proof-missing-semantic-invariants`
- `artifact-proof-invariant-id-not-cited`
- existing proof-depth fake fixtures for prose-only proof, missing transcripts, fake test names, missing hashes, and missing failing-first evidence

## Browser And Host Proof

Browser validation: N/A. SB01 changes validator scripts, bundle workflow instructions, and proof fixtures only; no UI routes, components, host startup behavior, or browser-visible behavior changed.

## Downstream Dependency Check

`bundle://proof/SB01/transcripts/prepared-validator-after-sb01.txt` proves the current follow-up bundle remains structurally valid at prepared stage after portable source-reference conversion. `bundle://proof/SB01/transcripts/active-skill-sync-hashes.txt` proves the active Codex skill-root copies match the repository skill files before SB02 begins.
