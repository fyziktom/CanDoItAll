# SB12 Semantic Invariants

| Invariant | Result | Proof |
| --- | --- | --- |
| `SB12_INV_CLEANUP_001` Default capabilities are materialized only from the capability template pack; obsolete hardcoded Skill, Tool, and AI-context seed builders are not active. | `Passed` | `proof/SB12/transcripts/static-cleanup-scan.txt`, `tests/CanDoItAll.Tests.Unit/CapabilityMigrationCleanupGuardTests.cs`, `src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs` |
| `SB12_INV_GUARD_001` MAF runtime capability composition does not own private nested capability descriptor DTOs for skills, tools, or MCP servers. | `Passed` | `proof/SB12/transcripts/unit-capability-cleanup-regression.txt`, `proof/SB12/transcripts/static-cleanup-scan.txt` |
| `SB12_INV_ACCESS_001` Runtime suppression flows through `ICapabilityAccessPolicyEvaluator` and `EffectiveCapabilitySet`; runtime code does not reintroduce raw selector string matching. | `Passed` | `proof/SB12/transcripts/unit-capability-cleanup-regression.txt`, `proof/SB12/transcripts/static-cleanup-scan.txt`, `proof/SB11/transcripts/playwright-large-screen-regression.txt` |
| `SB12_INV_DIAGNOSTICS_001` External tool and MCP setup failures remain structured, bounded, masked, correlated, and repairable. | `Passed` | `proof/SB12/transcripts/unit-capability-cleanup-regression.txt`, `proof/SB12/transcripts/integration-seed-filter-api-workflow-regression.txt`, `proof/SB12/transcripts/static-cleanup-scan.txt` |
| `SB12_INV_DOCS_001` Developer documentation explains how to add skill, tool, MCP, exposure descriptor, access policy, setup-test, diagnostic, repair-flow, and managed seed version changes. | `Passed` | `Templates/README.md`, `proof/SB12/transcripts/documentation-review.txt` |
| `SB12_INV_COMPAT_001` Compatibility data needed to read existing persisted catalogs remains intact while new default seed content is template-backed. | `Passed` | `proof/SB12/transcripts/integration-seed-filter-api-workflow-regression.txt`, `proof/SB12/transcripts/static-cleanup-scan.txt` |
| `SB12_INV_VALIDATION_001` Final closure build, regression tests, static scans, documentation review, and bundle validator pass. | `Passed` | `proof/SB12/transcripts/unit-capability-cleanup-regression.txt`, `proof/SB12/transcripts/integration-seed-filter-api-workflow-regression.txt`, `proof/SB12/transcripts/component-setup-process-workflow-regression.txt`, `proof/SB12/transcripts/dotnet-build-solution.txt`, `proof/SB12/transcripts/bundle-validator.txt` |
| `SB12_INV_BROWSER_001` SB12 does not require new browser validation because it did not touch visible setup behavior; SB11 remains the large-screen UI proof. | `Passed` | `proof/SB11/transcripts/playwright-large-screen-regression.txt`, `proof/SB11/screenshots`, `proof/SB12/manifest.md` |

## Notes

- Small and medium viewport checks were intentionally skipped for this bundle execution per user instruction. SB12 did not add any new UI route or component behavior.
- The existing seed builder size exception is documented in `proof/SB12/manifest.md` and `proof/SB12/transcripts/file-size-scan.txt`.
