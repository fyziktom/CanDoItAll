# SB02 Proof Manifest

## Scope

- Subbundle: `SB02`
- Status: `Completed`
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`

## Evidence

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first.txt`
- Target-list transcript: `bundle://proof/SB02/transcripts/db-process-table-targets.txt`
- Reset transcript: `bundle://proof/SB02/transcripts/db-reset.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/db-after-counts.txt`
- Template reload transcript: `bundle://proof/SB02/transcripts/template-reload.txt`
- Preservation transcript: `bundle://proof/SB02/transcripts/non-process-preservation.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`

## Changed File Hashes

- `repo://Templates/Processes/processes/ai-assisted-change-delivery/definition.json` SHA-256 `13154f1a4872280a1765e9894139a42cdd2c81fb11d924843b6e3e35dfa83360`
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json` SHA-256 `a56f7f5618554228576de44fb25125dabafc85faebc5f1aa01efbef892aef41d`
- `repo://Templates/Processes/processes/dotnet-feature-function-implementation/definition.json` SHA-256 `0c13c156f339e205086eacb466a660897f171a61953b26a1ad0b5ed2505edbf9`
- `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json` SHA-256 `9a29c44deec8c24cb8947bfab3aa2c738158e92737370c54c832a65a0d62fc76`
- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEditorModels.cs` SHA-256 `b606d26abf5f037e800f2656da6266237f3a07159fe6a1119582849242b6ba5f`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Support.cs` SHA-256 `aebfa7e6fd761f8ce4670b2f0f3c6bcc53b5d3b11b532c6e2dc37b10989d5247`
- `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateEditorModelFactory.cs` SHA-256 `7c0758eb9463ad4d24ae8c46ef871e6ce7c54a754ba2cbaf234bb98ac4729e78`
- `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackModels.cs` SHA-256 `7a3fda987787a0fe7ccc3030d833aeb6f0fe1e8bbf2975d5d039ca7e84e7de76`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/Dependencies/ProcessDependencyCompatibilityBridge.cs` SHA-256 `e145f2500bf0a99277c7c3cbd56a6cab02a4950a811d3116967b98b812804992`

## Closure

- Raw note owned: `N002`, `N003`
- Shipped behavior: Development process history/runs/outbox/activity history are cleared, current process templates are reloaded and published, and representative non-process settings are preserved.
- Source proof: `repo://src/CanDoItAll.Modules.Processes/Services/ProcessCatalogWarmupService.cs`; `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Support.cs`; `repo://Templates/Processes`
- Test proof: `bundle://proof/SB02/transcripts/template-reload.txt`
- Shallow-pass trap: readiness required all eight default definitions to be published, not merely imported.
- Adversarial negative proof: `bundle://proof/SB02/transcripts/failing-first.txt`
- Semantic positive proof: `bundle://proof/SB02/semantic-invariants.md`
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
