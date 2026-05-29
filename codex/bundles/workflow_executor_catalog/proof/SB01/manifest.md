# SB01 Proof Manifest

- Subbundle: `SB01`
- Status: `Completed`
- Owned requirements: R1, R12
- Raw notes: RN01, RN05
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed File Manifest

| File | After SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs` | `2C7F776012072868202F60CCA5826C1446720EDE715C3560A0330DA3ECB80DF3` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs` | `496FA09001B41BB6E753184B87A6571794F8A85B3B57B849AF3C2A1B2A633E1F` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs` | `9DCCA75E948F7448FB30D5C9A428E459FFD1176C7DFD6B72C7DA14F7BB3B5A4F` |
| `repo://tests/CanDoItAll.Tests.Unit/AgentFrameworkHostingServiceCollectionTests.cs` | `E3DE24C9CA3449619FE7FD6A1DE5EA824895040CBD1D2377B8A3F7B4A9B80849` |

Hash transcript: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-hosting-validator-missing-catalog.txt`
- Passing hosting/catalog transcript: `bundle://proof/SB01/transcripts/unit-hosting-validator-after-di-fix.txt`
- Passing validator behavior transcript: `bundle://proof/SB01/transcripts/unit-workflow-executor-validator-after-di-fix.txt`
- Source assertions: `bundle://proof/SB01/transcripts/source-assertions-validator-di.txt`
- Registration source assertions: `bundle://proof/SB01/transcripts/source-assertions-validator-di-registrations.txt`
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit-validator-di.txt`

## Failing-First And Passing Proof

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-hosting-validator-missing-catalog.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/unit-hosting-validator-after-di-fix.txt`
- Semantic positive proof: `bundle://proof/SB01/transcripts/unit-workflow-executor-validator-after-di-fix.txt`

## Source Assertions

- Product core registration injects `IWorkflowExecutorCatalog`: `repo://src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- Product module registration injects `IWorkflowExecutorCatalog`: `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- Template-pack validation can use a catalog when created by DI: `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs`
- Product-path regression test uses `IWorkflowCatalogService.SaveDefinitionAsync`: `repo://tests/CanDoItAll.Tests.Unit/AgentFrameworkHostingServiceCollectionTests.cs`

## Anti-Stub Audit

- Anti-stub transcript: `bundle://proof/SB01/transcripts/anti-stub-audit-validator-di.txt`
- Result: no TODO, `NotImplemented`, or `throw new NotSupportedException` markers were found in SB01 touched production/test paths.

## Downstream Smoke Proof

- Validator behavior smoke for planned, unknown, and invalid executor settings: `bundle://proof/SB01/transcripts/unit-workflow-executor-validator-after-di-fix.txt`
