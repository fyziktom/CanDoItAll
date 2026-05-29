# Proof Manifest SB01

## Status

- Subbundle: `SB01`
- Status: `Completed`
- Owned requirements: `R1-R12` baseline, missing-capability red proof, and regression gate.
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed File Hashes

| File | Before SHA-256 | After SHA-256 | Notes |
| --- | --- | --- | --- |
| `repo://codex/bundles/workflow_Office365_Scheduler/README.md` | unavailable; bundle repair started before baseline hash capture | `20113db2cb54c32628c6641459c7e6199c7e2e4f0531333941a79d563f820479` | Added validation summary and current observed head. |
| `repo://codex/bundles/workflow_Office365_Scheduler/bundle-manifest.json` | unavailable; bundle repair started before baseline hash capture | `435f6fbd1de116ec3b89433bb4a71f342f4d9457d899720913de8ff466103dfe` | Updated observed head. |
| `repo://codex/bundles/workflow_Office365_Scheduler/plan/01-phase-plan.md` | unavailable; bundle repair started before baseline hash capture | `30ca80406f57b67341487ce6fad2317bca0152c024ad21afebe49462c66a1877` | Added dependency map and gates. |
| `repo://codex/bundles/workflow_Office365_Scheduler/reviews/01-execution-report.md` | unavailable; bundle repair started before baseline hash capture | `d2a0b58c9277e2db03e0a63b208dcb6d2e6459a9ebb23777f03d388063e73d7e` | Seeded gate/browser/raw-note tables. |
| `repo://codex/bundles/workflow_Office365_Scheduler/subbundles/01-current-state-regression-and-gap-baseline/README.md` | unavailable; bundle repair started before baseline hash capture | `515d974efc66a17d7370b04607d64086099c31746e0030b581e33283be48156d` | Repaired SB01 contract. |

Hash transcript: `bundle://proof/SB01/transcripts/changed-file-hashes-sb01.txt`

## Command Transcripts

- Restore baseline passing: `bundle://proof/SB01/transcripts/restore-baseline.txt`
- Initial build blocked by local web process lock: `bundle://proof/SB01/transcripts/build-baseline.txt`
- Build baseline passing after stopping locked local web process: `bundle://proof/SB01/transcripts/build-baseline-after-unlocking-web.txt`
- Unit workflow/template baseline passing: `bundle://proof/SB01/transcripts/unit-workflow-baseline.txt`
- Component Scheduler/Workflows baseline passing: `bundle://proof/SB01/transcripts/component-scheduler-workflows-baseline.txt`
- Integration Scheduler/project workflow baseline passing: `bundle://proof/SB01/transcripts/integration-scheduler-project-workflow-baseline.txt`
- Semantic invariant evidence: `bundle://proof/SB01/transcripts/semantic-invariant-evidence.txt`

## Failing-First Evidence

- Failing-first missing-capability verifier: `bundle://proof/SB01/transcripts/failing-first-missing-office365-scheduler-capabilities.txt`

The verifier exits non-zero before implementation because the repo does not yet contain `office365.message-by-address-unprocessed`, `WorkflowInputParameterDescriptor`, or template `inputParameters`.

## Passing Evidence

- Restore/build/unit/component/integration proof passed in the transcripts listed above.
- Prepared-stage bundle validator passed before SB01 execution and after observed-head repair.

## Source Assertions

- Baseline source assertions: `bundle://proof/SB01/transcripts/source-assertions-baseline.txt`
- Current Office365 code contains category download and mark-processed paths.
- Scheduler Planner currently stores and validates raw `InputJson`.
- The red verifier confirms the requested typed Scheduler schema and address executor are absent before SB02/SB04.

## Anti-Stub Audit

- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit-baseline.txt`
- The scoped audit found no production `TODO`, `NotImplemented`, fixture-specific branching, or placeholder address-executor implementation in the Office365/Scheduler/template surfaces reviewed for SB01.

## Browser Or Host Proof

- Browser proof is not required for SB01 because this subbundle did not change browser-visible behavior.
- Host note: the first solution build failed because `CanDoItAll.Web` process `36996` locked output files; the process was stopped and the rerun build passed. The failure and passing rerun are both preserved.

## Downstream Smoke

- Unit, component, and integration baselines passed for the workflow/template/Scheduler surfaces that downstream subbundles will change.

