# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| R001/N001/N002/N003 | `requirements/01-normalized-requirements.md` | `subbundles/01-manager-audio-auto-speak-parity` | Targeted component test for Manager voice-mode send auto-synthesis | Manual read remains covered by existing path. |
| R003/R004/N004/N005 | `requirements/01-normalized-requirements.md` | `subbundles/02-manager-selected-run-usage-context` | Targeted component test for Manager tab load options and prompt usage metrics | Uses `ProcessRuntimeWorkspaceProjection.Stats`. |
| R005 | `requirements/01-normalized-requirements.md` | `subbundles/02-manager-selected-run-usage-context` | `ProcessManagerChatPromptClassifierTests` | Prevents runtime tools from being disabled for natural cost/token questions. |
| Closure proof | `plan/01-phase-plan.md` | `subbundles/03-proof-restart-and-browser-demo` | Build, restart 5032, browser proof | Final gate for user-visible behavior. |
