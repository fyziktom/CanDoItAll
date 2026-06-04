# SB11 Semantic Invariants

- Invariant ID: `SB11-INV-001`
- Source raw note: "Preserve mandatory vs optional source-line handling."
- Expected behavior: Project-structure downgrade/defer/drop detection moves to helper rules without changing mandatory source-line preservation or optional/deferred source-line filtering.
- Disallowed shallow implementation: Moving tokenization but leaving source-line optionality or weakening phrase rules in the dispatcher.
- Passing test: `bundle://proof/SB11/transcripts/focused-unit-architecture-tests.txt` and `bundle://proof/SB11/transcripts/focused-project-structure-preservation-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectStructureRequirementValidationRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB11/source-assertions/project-structure-preservation-rule-extraction.md`
- Red-team negative case: Integration tests prove weakened mandatory requirements are flagged while deferred source lines are ignored; source scans prove no helper side effects or prohibited dependencies.
- Downstream dependency check: SB12 may start because architecture and project-structure preservation parity tests passed.

- Raw note owned: Extract project-structure requirement preservation rules.
- Shipped behavior: Downgrade/defer/drop preservation remains equivalent; 2 focused integration tests passed.
- Source proof: `bundle://proof/SB11/source-assertions/project-structure-preservation-rule-extraction.md`
- Test proof: `bundle://proof/SB11/transcripts/focused-project-structure-preservation-integration-tests.txt`
- Shallow-pass trap: Helper exists but optional source-line filtering no longer protects intentionally deferred requirements.
- Adversarial negative proof: `bundle://proof/SB11/transcripts/focused-project-structure-preservation-integration-tests.txt`
- Semantic positive proof: `bundle://proof/SB11/transcripts/focused-project-structure-preservation-integration-tests.txt`
- Anti-stub audit: `bundle://proof/SB11/transcripts/project-structure-rule-source-scans.txt`
