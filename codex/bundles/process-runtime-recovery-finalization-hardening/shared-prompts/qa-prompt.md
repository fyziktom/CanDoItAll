# QA Prompt

Use this prompt to review a completed subbundle or the final initiative closure.

```text
Review the completed work against codex/bundles/process-runtime-recovery-finalization-hardening.

Check coverage:
- Map changed behavior back to requirements/01-normalized-requirements.md.
- Verify user stories and exception paths in requirements/02-user-stories-and-exceptions.md are covered or explicitly deferred.
- Confirm the assigned subbundle acceptance checklist and progression gate are satisfied.

Check architecture:
- Confirm generic runtime remains domain-neutral.
- Confirm dependency direction matches architecture/02-csharp-dependency-direction.md.
- Confirm new contracts are strongly typed.
- Confirm no new silent fallback or unsafe automatic retry was introduced.
- Confirm ProcessRuntimeEngine and AgentFrameworkProcessExecutionAdapter partial clusters were not expanded as the final architecture.
- Confirm driver-specific policy is isolated behind driver contracts or concrete drivers.

Check proof:
- Review failing-first proof for critical changes.
- Review passing test transcripts.
- Review source assertions and CodeAnalytics dependency proof when architecture changed.
- Reject positive proof that manually seeds runtime state and bypasses production paths for critical scenarios.
- Reject proof that asserts only step status while missing artifact lineage, finalization receipt, manager handoff, or recovery route facts.

Check browser or host validation:
- If UI/projection/host-visible behavior changed, verify Playwright or host evidence is recorded in reviews/01-execution-report.md.
- If no browser-visible behavior changed, verify the report states N/A with rationale.

Blocker handling:
- If a prerequisite is missing, mark the subbundle blocked and route back to the prerequisite owner.
- If a raw architect note remains only partially solved, require explicit residual risk and follow-up ownership.
- If proof is weak, reopen the subbundle before downstream work proceeds.
```
