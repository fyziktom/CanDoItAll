# SB01 proof manifest

- Subbundle: `SB01`
- Status: `Completed`
- Owned requirements: R1, R10
- Raw notes: Previous bundle deferred MAF package migration; this follow-up must establish an intentional current MAF package/API baseline before further workflow runtime hardening.
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `1f8b61f550fdfb2bd2018b4feed8b4c7c462042dc75330e3f50e2e2eaa2e7b9c` | `bf3a9692657d4a773db2df110941d3d8e8f3f9d3e3ae0b22e95d7a6803d0844b` |
| `repo://src/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj` | `2656b7f5ee04b95ad9bae61b918dae096eb4f42a9131ec42079a9291e2f92494` | `768acce32aa2f50b193064a7d84c84a35e97f7a0556d4906709ca6c1e0e484dd` |
| `repo://src/CanDoItAll.AgentFramework.Maf/README.md` | `e66f560988316c669ad7de55dd9b1ee40b46a3ddb8a65f181f56467e1c20d078` | `582ff1a03ce728f170ee0a826d30b24e2520629b96f5470e4a97256acd611a4d` |
| deleted `tests/CanDoItAll.Tests.Unit/Maf16CapabilityReflectionTests.cs` | `c7de908a07b3f7cf191bd379ecaabb30797abb16482ca9d0a0bf21efe2e42e46` | `N/A` |
| `repo://tests/CanDoItAll.Tests.Unit/MafPackageBaselineReflectionTests.cs` | `N/A` | `74cfaffa4d9cc8d3866958a2ab4994395f58c61c76f554152a3f7fd6d2b39eb3` |

Hash transcript: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`

## Command Transcripts

- Package scan before upgrade: `bundle://proof/SB01/transcripts/package-outdated-baseline.txt`
- Restore baseline: `bundle://proof/SB01/transcripts/restore-baseline.txt`
- Build baseline: `bundle://proof/SB01/transcripts/solution-build-baseline.txt`
- Unit baseline: `bundle://proof/SB01/transcripts/unit-workflow-baseline.txt`
- Integration baseline: `bundle://proof/SB01/transcripts/integration-workflow-baseline.txt`
- Component baseline: `bundle://proof/SB01/transcripts/component-workflow-baseline.txt`
- Restore after upgrade: `bundle://proof/SB01/transcripts/restore-after-maf18-upgrade.txt`
- Build after upgrade: `bundle://proof/SB01/transcripts/build-after-maf18-upgrade.txt`
- Package scan after upgrade: `bundle://proof/SB01/transcripts/package-outdated-after-maf18-upgrade.txt`
- Passing unit proof: `bundle://proof/SB01/transcripts/unit-workflow-after-maf18-upgrade-passing-rebuilt.txt`
- Passing integration proof: `bundle://proof/SB01/transcripts/integration-workflow-after-maf18-upgrade.txt`
- Passing component proof: `bundle://proof/SB01/transcripts/component-workflow-after-maf18-upgrade.txt`
- Semantic invariant index: `bundle://proof/SB01/transcripts/semantic-invariant-evidence.txt`

## Failing-First And Passing Proof

- Failing-first transcript: `bundle://proof/SB01/transcripts/unit-workflow-after-maf18-upgrade.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/unit-workflow-after-maf18-upgrade-passing-rebuilt.txt`

## Source Assertions

- Source-level assertion transcript: `bundle://proof/SB01/transcripts/source-assertions-after-doc-update.txt`
- Package references: `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` and `repo://src/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj`
- Runtime documentation: `repo://src/CanDoItAll.AgentFramework.Maf/README.md`
- Reflection test: `repo://tests/CanDoItAll.Tests.Unit/MafPackageBaselineReflectionTests.cs`

## Anti-Stub Audit

- Anti-stub transcript: `bundle://proof/SB01/transcripts/anti-stub-audit-after-doc-update.txt`

## Downstream Smoke Proof

- Workflow API smoke: `bundle://proof/SB01/transcripts/integration-workflow-after-maf18-upgrade.txt`
- Workflow component smoke: `bundle://proof/SB01/transcripts/component-workflow-after-maf18-upgrade.txt`
