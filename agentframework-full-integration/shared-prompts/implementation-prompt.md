# Shared Prompt — Implementation

```text
You are executing the bundle `candoitall-agentframework-full-integration-2026-04-14`.

Mission:
Integrate CanDoItAll.AgentFramework into CanDoItAll as native code, while also adding Collaboration, staged process launch, provider ownership cleanup and CRM-HR resource binding. Follow the bundle exactly. Do not improvise around source-of-truth boundaries.

Hard rules:
1. Work only inside `C:\repositories\CanDoItAll`. Use `C:\repositories\CanDoItAll.AgentFramework` only as source material to copy from.
2. Never add a live project reference back to the external AgentFramework repo.
3. Do not continue past a subbundle until its progression gate passes.
4. If a subbundle reveals messy architecture, duplicate writes, oversized files or split source-of-truth, stop and create a refactor subbundle before continuing.
5. Keep new files focused. Prefer small services, bridges and mappers over god classes.
6. Reuse existing platform helpers: `IClock`, `IActivityStream`, `SecretService`, `IAutomationMessagePublisher`, `ProcessOutboxService`, `IStorageCatalogService`, `ISearchIndexService`.
7. Collaboration must be the canonical human-visible inbox/thread store.
8. Processes must remain the canonical owner of role messaging policy and launch planning.
9. CRM-HR must remain the canonical owner of resource identities.
10. AgentFramework must become the canonical owner of technical agent definitions and AI runtime execution.
11. No direct agent-to-agent communication may bypass process messaging policy.
12. No fake tests, no seeded shortcut states used as proof.

Execution pattern:
- Read `analysis/`, `architecture/`, `plan/` and the current subbundle README.
- Implement only the current subbundle scope.
- Run the exact proof required by that subbundle.
- Update execution evidence before moving to the next subbundle.
- Reopen earlier work if any reopen trigger fires.

Special caution:
- The source repos currently show 8 built-in scenarios (`SC01–SC08`). Respect that actual inventory and extend it with the new scenarios from the bundle instead of assuming there are only 5.
- Do not import the AgentFramework sandbox host shell as a second app. Recompose its functionality inside CanDoItAll.Web.
- Do not leave the old Workspace provider execution path active in parallel “for now” without a kill switch and a cleanup plan.

Definition of done for each subbundle:
- Acceptance checklist complete.
- Proof artifacts captured.
- Browser validation logged if UI changed.
- Downstream dependencies remain trustworthy.
```
