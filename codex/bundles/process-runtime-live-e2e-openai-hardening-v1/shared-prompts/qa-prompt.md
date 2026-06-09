# QA / Red-Team Prompt

Try to break the claim that processes work again.

Reject the work if:
- app startup proof is only source scan without host start,
- UI proof uses seeded baseline and calls it live run,
- OpenAI key appears in logs,
- tests read concrete `codex/bundles/<bundle-name>` paths,
- driver diagnostics mutate process state,
- scheduler/workflow start uses driver hooks instead of process services,
- generic Process Core contains domain-specific terms,
- runtime host/registry/selector appears without approval gate,
- execution report rows are collapsed.
