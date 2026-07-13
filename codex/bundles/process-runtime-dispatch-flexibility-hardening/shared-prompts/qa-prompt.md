# QA Prompt

Review the selected completed subbundle as a gatekeeper.

Check these first:

- The subbundle status, proof manifest, semantic invariant contract, and execution report row agree.
- Every changed behavior has a failing-first or explicit non-production exemption and passing proof.
- Existing behavior from commit `6775de820 phase1` is preserved.
- The implementation improved boundaries instead of only moving methods into equally broad classes.
- Generic runtime paths remain free of software-delivery, .NET, repository, screenshot, and project-structure assumptions unless the path is explicitly a domain contributor.
- Prompt fragment composition, completion evidence policy, and actual step execution dispatch behavior live behind driver-owned ports or driver implementations, not generic runtime/application private helpers.
- Source/project-reference scans prove `src/Processes/*` does not reference MAF, AgentFramework implementation projects, or `CanDoItAll.Modules.AgentFramework`.
- Diagnostics are actionable and include run id, step id, step key, policy, or tool context as applicable while masking sensitive values.

Critical proof audit:

- Reject proof that only shows file existence, passing build, non-empty output, or a table row.
- Reject completed critical subbundles without `proof/SBxx/manifest.md` and `proof/SBxx/semantic-invariants.md`.
- Reject product completion proof that manually seeds production-only signals or omits current-run receipt/lifecycle evidence.
- Reject prompt proof if generic non-software process prompts still include AgentFramework or .NET-specific guidance.
- Reject dispatch proof if the dispatcher hardcodes AgentFramework/MAF provider, prompt, evidence, or driver-specific recovery behavior.
- Reject dependency proof if it only checks project files and skips source namespace imports, or only checks source imports and skips project references.

Browser and host proof:

- Use browser proof only when UI/API/dashboard behavior changed.
- If UI changed, require a large-screen pass first, screenshot review against explicit questions, and narrower viewport pass when layout is affected.
- If filesystem/product-root policy changed, require host/file-system proof for accepted and rejected path cases.

Closure:

- Mark raw notes as `Solved`, `Partially solved`, or `Not solved` only with proof citations.
- Reopen the affected subbundle when proof is weak instead of hiding the gap in residual risks.
