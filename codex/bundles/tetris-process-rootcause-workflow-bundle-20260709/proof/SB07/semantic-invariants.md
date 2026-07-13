# SB07 Semantic Invariants

- Invariant ID: `SB07-INV-template-migration`
- Source raw note: GPTPro RC6 and the user requirement to audit all similar process and artifact templates, not just the Tetris example.
- Expected behavior: Impacted accepted/repair templates get branch-aware validation metadata, while templates with different semantics have explicit exemption reasoning.
- Disallowed shallow implementation: Updating only `software-delivery` or storing routing behavior in the generic adapter.
- Failing-first test: `bundle://proof/shared/transcripts/failing-first.txt`
- Passing test: `Enrich_adds_root_blazor_delivery_branch_aware_validation_metadata` in `bundle://proof/shared/transcripts/passing-tests.txt`
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs`
- Production assertions: Workbench launch variables emit branch-aware receipt maps, content checks, and route maps for software-delivery and Blazor delivery roots.
- Red-team negative case: dotnet-development-slice, dotnet-solution-setup, and screenshot writeback remain exempt where the incident acceptance-browser proof failure does not apply.
- Downstream dependency check: SB04 consumes these route maps and SB08 consumes the same contributor for acceptance criteria.
