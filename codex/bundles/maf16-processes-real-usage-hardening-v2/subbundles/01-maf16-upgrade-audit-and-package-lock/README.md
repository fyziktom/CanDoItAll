# SB01: MAF 1.6 Upgrade Audit And Package Lock

## Status

- Completed

## Objective

Audit package versions, lock/restore state, and remaining MAF 1.3 references.

## Covered Inputs

- RQ01: prove the MAF 1.6 upgrade is stable and not only a package bump.

## Prerequisites

- Prepared-stage bundle validation must pass.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj
- repo://src/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj
- bundle://analysis/02-maf16-official-notes.md

## Deliverables

- Package/version matrix covering core, OpenAI, Workflows, A2A, Hosting A2A, and remaining 1.3 checks.
- Restore/build proof and active-source grep proof.

## Dependency Impact

- This is the package baseline for every MAF adapter and process-runtime subbundle.

## Validation Depth

- Run restore/build and grep audits.
- Critical semantic proof must include package-only versus adapter/process/UI classification.

## Implementation Steps

- Confirm package references and resolved assets.
- Record A2A preview status and reason.
- Fail on active MAF 1.3 references in `src` or `tests`.
- Update `proof/SB01` with transcripts, hashes, source assertions, and anti-stub audit.

## Do Not Do

- Do not change package versions without source-backed reason.
- Do not treat package references as runtime adoption proof.

## Acceptance Checklist

- MAF package matrix exists.
- Restore/build proof is captured.
- MAF 1.3 negative audit is captured.
- `proof/SB01/manifest.md` and `proof/SB01/semantic-invariants.md` are updated.

## Proof Required

- Failing-first or adversarial proof under `bundle://proof/SB01/transcripts/failing-first.txt`.
- Passing proof under `bundle://proof/SB01/transcripts/passing.txt`.
- Source assertions, anti-stub audit, and changed-file hashes under `bundle://proof/SB01/transcripts`.

## Browser Validation Logging

- N/A - no browser-visible behavior in this subbundle.

## Progression Gate

- SB02 may start only after package baseline proof is recorded and no active MAF 1.3 references remain.

## Suggested Agent Prompt

Audit the local MAF package references and prove the upgrade baseline with restore/build, package matrix, source assertions, and negative grep proof.

