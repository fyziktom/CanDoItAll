# SB01 Semantic Invariants

## Invariant MAF-SB01-DOMAIN-NEUTRAL

- Invariant ID: `MAF-SB01-DOMAIN-NEUTRAL`
- Source raw note: common MAF workspace image prompts leaked software-development and UI-design assumptions.
- Expected behavior: common image prompts describe observable image evidence only, while caller-supplied prompts remain trimmed and unchanged.
- Disallowed shallow implementation: replacing only the named example while leaving other common MAF helpers or capability templates with screenshot, UI, browser, software, or design assumptions.
- Failing-first test: `bundle://proof/SB01/transcripts/adversarial-negative.txt` proves the forbidden-domain scan stays empty.
- Passing test: `NormalizeSingleImagePrompt_WhenPromptIsEmpty_UsesDomainNeutralVisibleEvidencePrompt` in `bundle://proof/SB01/transcripts/passing.txt`.
- Changed source files: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceImageAnalysisPromptNormalizer.cs` with hash `1B6E25E5F8785E5D647E3BFE78C600A03BB72DEEA22FC3BB17864454D2475FD8`.
- Production assertions: `WorkspaceRuntimePlugin`, `InputAttachmentPreparer`, evidence builder wording, and workspace image tool descriptions now delegate to domain-neutral wording.
- Red-team negative case: a common helper that still says software delivery agent, UI state, screenshot files, or software behavior would be found by the negative scan.
- Downstream dependency check: SB05 owns development-specific image guidance through a scoped capability, not through common MAF defaults.
