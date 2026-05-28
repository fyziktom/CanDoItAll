# SB03 Proof Manifest

- Subbundle: `SB03`
- Proof type: run folder artifact projection.
- Portable source references: `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAssemblyService.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`.
- Semantic invariant contract: `proof/SB03/semantic-invariants.md`.
- Passing command: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName=CanDoItAll.Tests.Integration.ProjectWorkbenchServiceIntegrationTests.GetStructureAsync_projects_process_run_output_folders_into_the_structure_surface"`.
- Passing transcript: `bundle://proof/SB03/transcripts/passing.md`.
- Anti-stub transcript: `bundle://proof/SB03/transcripts/anti-stub.md`.
- Failing-first: N/A process projection proof; adversarial negative proof is the date-receipt path fixture.
- Changed-file SHA-256: `4465A1EEBBE3285533042EA732421051EA83231B6420698FCFD63F4A150D64EC` for `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAssemblyService.cs`.
- Changed-file SHA-256: `424A1F88FF937F77CD9CAB0B471508479C78D85532E0DC2E6DB1E1E14465F2D5` for `repo://tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`.
