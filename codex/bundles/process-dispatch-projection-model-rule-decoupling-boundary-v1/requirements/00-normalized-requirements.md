# Normalized Requirements

| ID | Requirement | Acceptance proof |
| --- | --- | --- |
| RQ-001 | Preserve all original artifact projection behavior. | Focused integration projection tests and source-family order proof pass. |
| RQ-002 | Do not create Process Core yet. | Source scan for `CanDoItAll.Processes.Core` and related namespaces returns no production matches. |
| RQ-003 | Do not create production driver APIs yet. | Source scan for `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, `ProcessDriverPack` returns no production matches. |
| RQ-004 | Replace direct coordinator dependency on dispatcher nested models with module-local projection models where safe. | Source scan after migration gates shows projection coordinators no longer use aliases to `ProcessRunAutomationDispatchService.DispatchCandidate`, `DispatchArtifactExpectation`, `SessionFileContent`, `ProcessMockArtifactProjection`, or `ArtifactProjectionLineage`, except in the dispatcher adapter boundary. |
| RQ-005 | Keep dispatcher adapter/factory as the only translation boundary from `ProcessRunAutomationDispatchService.*` nested models. | Architecture tests and source assertions identify exactly allowed adapter files. |
| RQ-006 | Extract projection static helper forwarding into module-local rule classes where behavior can remain identical. | Rule helper tests and source assertions prove no branch-order or semantics drift. |
| RQ-007 | Preserve projection source-family order. | Unit architecture test verifies execution → process mock → workspace-written → existing managed → response text → provider-native browser → completed decision. |
| RQ-008 | Preserve file IO and storage side effects as explicit side-effect facets. | Source scans and tests show no hidden file IO in pure rule classes. |
| RQ-009 | Preserve candidate-state mutation semantics. | Candidate state mutation tests cover external reference keys and recorded expectation ids. |
| RQ-010 | Keep browser validation N/A for service-only work and do not create mobile/small/medium proof. | No UI/prohibited viewport proof scan passes. |
