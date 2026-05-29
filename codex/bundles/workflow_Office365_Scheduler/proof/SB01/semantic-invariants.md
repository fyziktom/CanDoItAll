# Semantic Invariants SB01

## SB01-INV-BASELINE

- Invariant ID: `SB01-INV-BASELINE`
- Source raw note: R1-R12 require a stable baseline before Office365 and Scheduler implementation.
- Expected behavior: The existing workflow executor catalog, template loader, Scheduler, and project workflow slices restore, build, and pass targeted tests before feature edits.
- Disallowed shallow implementation: Claiming baseline stability from file existence or prose without running restore, build, and targeted tests.
- Failing-first test: `bundle://proof/SB01/transcripts/build-baseline.txt` captured the host lock failure rather than hiding it.
- Passing test: `bundle://proof/SB01/transcripts/build-baseline-after-unlocking-web.txt`, `bundle://proof/SB01/transcripts/unit-workflow-baseline.txt`, `bundle://proof/SB01/transcripts/component-scheduler-workflows-baseline.txt`, and `bundle://proof/SB01/transcripts/integration-scheduler-project-workflow-baseline.txt`.
- Changed source files: No production source files changed in SB01; bundle repair hashes are in `bundle://proof/SB01/transcripts/changed-file-hashes-sb01.txt`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions-baseline.txt` identifies current Office365 category executors and Scheduler raw `InputJson` behavior.
- Red-team negative case: `bundle://proof/SB01/transcripts/failing-first-missing-office365-scheduler-capabilities.txt` prevents closing from a false assumption that the requested capabilities already exist.
- Downstream dependency check: SB02 can start because the existing workflow/template/Scheduler regression baseline passed after the local web process lock was cleared.

## SB01-INV-MISSING-CAPABILITY-RED

- Invariant ID: `SB01-INV-MISSING-CAPABILITY-RED`
- Source raw note: The original request asks for a new address-based Office365 email polling executor and typed Scheduler setup.
- Expected behavior: Before implementation, the repo should show these capabilities missing so SB02 and SB04 have real red evidence.
- Disallowed shallow implementation: Treating a category-based executor or raw `InputJson` editor as satisfying address polling or typed Scheduler setup.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-missing-office365-scheduler-capabilities.txt`.
- Passing test: `bundle://proof/SB01/transcripts/semantic-invariant-evidence.txt` records that the failing-first condition is tied to this invariant.
- Changed source files: No production source files changed in SB01.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions-baseline.txt` shows current category-centric Office365 executors and raw Scheduler input storage.
- Red-team negative case: Searching production source and templates for the requested executor id and typed schema contract exits non-zero before implementation.
- Downstream dependency check: SB02 must implement the missing executor and SB04 must implement the missing typed schema; later subbundles must not close by citing SB01 red proof as shipped behavior.

