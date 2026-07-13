# SB02 Proof Manifest

## Scope

- Workspace tool wiring.
- DI and fallback resolver registration.
- Python MarkItDown command-path removal.
- Receipt behavior preservation.

## Changed File Hashes

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceArtifactToolService.cs` SHA-256 `19EFDFD2559B71FB1124BBA0E97A09FD387BD045F2A4E6871A942E95CF0A4201`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileReceiptWriter.cs` SHA-256 `5BB2E2BAD67039F70500375AF5AA2440E94E35A0ED2267F759B98E7812D8836E`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs` SHA-256 `C8667322D01463D33279097A377AB6B22B187B612774A85155FC8F6D3F7CD02D`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeDependencyResolver.cs` SHA-256 `A5070E9207EAC7381079B489E264A1542D0106A705C73760BEAC8FBA45E09938`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs` SHA-256 `108DBEDB288623F912DFB36B877EE96156BC8B3AAB8A4546FA49F8564ABD9C06`

## Semantic Contract

- `bundle://proof/SB02/semantic-invariants.md`

## Evidence

- Passing transcript: `bundle://proof/SB02/transcripts/passing-workspace-tool-tests.log`
- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-receipt-path.log`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.log`

## Result

- Passed.
