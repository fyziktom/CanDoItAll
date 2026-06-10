# SB018 Gate F Proof Manifest

## Status
Passed.

## Gate Scope
- P06 host options and emergency disable.
- Adds validated verification host options.
- Adds host-level emergency disable, per-lane enable/disable, selected-lane payload item limits, and supplied evidence content byte limits.

## Changed File Hashes
| Artifact | SHA256 |
| --- | --- |
| src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs | 112054db257fd56b46cc8fa13bac291fd2dbf6c1a53b89a670de48ccfaf32e75 |
| src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHostModels.cs | 9bfed605a46c11eb08f22745b62176a671c310929611a6ea57228f3ba9faa698 |
| src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHostOptions.cs | 342ae2634d7e1ba04d3871be93d6ecaa24179e35baff230bb109fd104b394a55 |
| src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs | db12e1c019c1aa0dbae1c4cfd4ed7c591453d59505c78f86c285bff2807a6c86 |
| tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs | 0d1b88a72a16a6f05b3d5d20fd3336fecc6be99c43ac9580695a70cd257f058b |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/proof/SB016/transcripts/host-options-policy-focused-tests.txt | e3470d9ab849d1d31972446c92f40372f6d97144bdbb875f05e8456114a91074 |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/proof/SB016/transcripts/host-options-validation-source-assertions.txt | d7d896409648d0668509d0d8da808325a140626ad9f3a3ee2589c47cbd1a51cb |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/proof/SB017/transcripts/host-options-policy-source-assertions.txt | 4e4424f7524bb987976abca205df224bf9de4bb537dcace0a06d325571480abc |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/proof/SB018/transcripts/gate-f-source-diff-and-anti-stub-audit.txt | c1d76304007f70d947d4f568a226f4a20916b9cad725699cef459103bfd6f22a |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/proof/SB018/transcripts/red-team-options-policy-shallow-proof-rejection.txt | 2bd59b10e68190b8e27e06e1045dabcced21b498fdeaa0b8b5366b18495cf554 |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/subbundles/SB016/README.md | 5b35506fc0059bd97d93fb471cdb64c714101f3ea186204229183a2f8ff2b5e4 |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/subbundles/SB017/README.md | 432dcd60c7a302148cef94b1dc528d0d5c5153695a4b8e6c065646fe039de2b7 |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/subbundles/SB018/README.md | 4e0a0108d7309078abefdda7cd5ed8134c63c7cd76321a68a5e0ff0b00766f1b |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/reviews/01-execution-report.md | d9d928e7f4023909b215f7c869492cfeac5ac4913e2504d942990fdc3104a3ef |

## Production Behavior Artifact Matrix
| Artifact | Classification | Gate F conclusion |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHostOptions.cs` | Options model | Defines `Processes:VerificationRuntimeHost`, host enable switch, typed lane switches, and validated item/content limits. |
| `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | DI/options registration | Binds options in full module registration and validates defaults in the host helper used by tests and DI. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs` | Runtime enforcement | Denies disabled host/lane requests before orchestration and denies payload count/content byte limit breaches with structured mutation-free denials. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHostModels.cs` | Denial taxonomy | Adds `HostDisabled`, `LaneDisabled`, `PayloadLimitExceeded`, and `SuppliedEvidenceContentLimitExceeded` denial codes. |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs` | Focused policy tests | Proves options validation, emergency disable, lane disable, payload count limit, content byte limit, and unchanged host success path. |

## Proof Artifacts
- Focused host options tests: `bundle://proof/SB016/transcripts/host-options-policy-focused-tests.txt`.
- SB016 options validation source assertions: `bundle://proof/SB016/transcripts/host-options-validation-source-assertions.txt`.
- SB017 options policy source assertions: `bundle://proof/SB017/transcripts/host-options-policy-source-assertions.txt`.
- SB018 source diff and anti-stub audit: `bundle://proof/SB018/transcripts/gate-f-source-diff-and-anti-stub-audit.txt`.
- SB018 red-team rejection: `bundle://proof/SB018/transcripts/red-team-options-policy-shallow-proof-rejection.txt`.

## Gate F Result
Passed. The verification host has validated options, emergency disable, exact lane disables, and bounded selected-lane payload processing without fallback selection, live-provider coupling, or process mutation authority.
