# SB01 semantic invariants

## SB01-MAF18-BASELINE

- Invariant ID: `SB01-MAF18-BASELINE`
- Source raw note: R1 requires a fresh MAF package/API baseline that is upgraded to the latest compatible line or blocked by exact ADR evidence; R10 requires an explicit executor strategy decision surface to remain test-covered after the package move.
- Expected behavior: MAF package references use the verified 1.8 compatible baseline, the A2A preview packages are aligned to the matching preview line, restore/build succeeds, and reflection proof asserts the loaded Microsoft Agent Framework workflow assemblies are from the 1.8 line.
- Disallowed shallow implementation: Updating only documentation or project-file text without restore/build/test proof, or keeping a reflection test that still asserts the old 1.6 assembly line.
- Failing-first test: `bundle://proof/SB01/transcripts/unit-workflow-after-maf18-upgrade.txt` shows the stale 1.6 reflection assertion failed after package upgrade.
- Passing test: `bundle://proof/SB01/transcripts/unit-workflow-after-maf18-upgrade-passing-rebuilt.txt` shows the rebuilt workflow unit slice passes with `MafPackageBaselineReflectionTests`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`, `repo://src/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj`, `repo://src/CanDoItAll.AgentFramework.Maf/README.md`, and `repo://tests/CanDoItAll.Tests.Unit/MafPackageBaselineReflectionTests.cs`; hashes are in `bundle://proof/SB01/transcripts/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions-after-doc-update.txt` verifies the package references and README baseline use 1.8 package values.
- Red-team negative case: `bundle://proof/SB01/transcripts/unit-workflow-after-maf18-upgrade.txt` proves the previous 1.6-specific reflection test fails against the upgraded assemblies instead of silently accepting a stale baseline.
- Downstream dependency check: `bundle://proof/SB01/transcripts/integration-workflow-after-maf18-upgrade.txt` and `bundle://proof/SB01/transcripts/component-workflow-after-maf18-upgrade.txt` prove workflow API and component slices still pass before SB02 starts.

