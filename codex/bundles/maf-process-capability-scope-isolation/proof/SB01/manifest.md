# SB01 Proof Manifest

## Subbundle

- Subbundle: `SB01`
- Status: `Completed`
- Owned requirement: common MAF workspace image analysis must not inject software, UI, browser, or design-domain assumptions.
- Test name: `NormalizeSingleImagePrompt_WhenPromptIsEmpty_UsesDomainNeutralVisibleEvidencePrompt`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceImageAnalysisPromptNormalizer.cs` | `1B6E25E5F8785E5D647E3BFE78C600A03BB72DEEA22FC3BB17864454D2475FD8` |

## Proof Artifacts

- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB01/transcripts/adversarial-negative.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/passing.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub.txt`
- Source assertion: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceImageAnalysisPromptNormalizer.cs`
- Source assertion: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Input/InputAttachmentPreparer.cs`
- Source assertion: `repo://Templates/Capabilities/tools.json`

## Closure

- Failing-first: `bundle://proof/SB01/transcripts/adversarial-negative.txt` records the forbidden common-domain wording scan with no matches.
- Semantic positive proof: `bundle://proof/SB01/transcripts/passing.txt` records the focused prompt tests.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub.txt` records no placeholder implementation in the changed prompt path.
