# SB01 Proof Manifest

## Scope

- Converter contract and implementation.
- ManagedCode.MarkItDown package integration.
- Direct converter behavior.

## Changed File Hashes

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceDocumentMarkdownConverter.cs` SHA-256 `ABA47D3B5604EFC222085E659B63BB418E9423D8B1ED551A3E6529FD929B3E96`
- `repo://src/MAF/Tools/CanDoItAll.Tools.Documents/Markdown/ManagedCodeMarkItDownDocumentMarkdownConverter.cs` SHA-256 `5B2C73FE58996E5AA9122575DF65A7C6520A921BED7C4C14843D65A1B0A4E40E`
- `repo://src/MAF/Tools/CanDoItAll.Tools.Documents/CanDoItAll.Tools.Documents.csproj` SHA-256 `1B2C9A1055AC68243CD8E8CDF8A5ADBC0EFF1606EC0C6104B2921F164B7C7EB5`

## Semantic Contract

- `bundle://proof/SB01/semantic-invariants.md`

## Evidence

- Passing transcript: `bundle://proof/SB01/transcripts/passing-converter-tests.log`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.log`
- Failing-first: N/A process - this subbundle introduced a new concrete converter and no pre-change failing command transcript was captured; negative behavior is covered by the missing-source test.

## Result

- Passed.
