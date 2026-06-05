# SB24 Semantic Invariants

- Invariant ID: SB24-DEDUPE-PARITY
- Source raw note: Dedupe managed path/project-structure/governed inspection helpers without branch-order drift.
- Expected behavior: Scoped managed paths, project-structure required paths, path scoring, governed inspection path sets, and upstream inspection summaries are helper-owned and tested.
- Disallowed shallow implementation: Moving code behind wrappers while changing artifact branch order, introducing Process Core or driver APIs, hiding side effects inside pure-looking helpers, or adding UI/mobile proof artifacts is rejected.
- Failing-first test: N/A - process non-production refactor with no behavior change; shallow implementation is rejected by bundle://proof/shared/transcripts/no-core-no-driver-no-ui-scan.txt, bundle://proof/shared/transcripts/anti-stub-scan.txt, and focused regression tests.
- Passing test: bundle://proof/shared/transcripts/focused-integration-tests.txt and bundle://proof/shared/transcripts/focused-unit-boundary-tests.txt.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs plus helper files listed in bundle://proof/SB24/manifest.md.
- Production assertions: bundle://proof/shared/transcripts/build-slnx-no-restore.txt, bundle://proof/shared/transcripts/line-count-and-source-scans.txt, and bundle://proof/shared/transcripts/no-core-no-driver-no-ui-scan.txt show preserved buildability, target line-count closure, and no forbidden production boundary drift.
- Red-team negative case: bundle://proof/shared/transcripts/no-core-no-driver-no-ui-scan.txt verifies no Process Core, driver pack, driver registry, driver descriptor, UI file change, or prohibited viewport proof artifact was introduced.
- Downstream dependency check: bundle://proof/shared/transcripts/focused-integration-tests.txt covers artifact classification, provider-native browser output, critical failure suppression, completed-decision metadata, project-structure paths, and governed inspection paths; bundle://proof/shared/transcripts/focused-unit-boundary-tests.txt covers helper locality and source-boundary assertions.
