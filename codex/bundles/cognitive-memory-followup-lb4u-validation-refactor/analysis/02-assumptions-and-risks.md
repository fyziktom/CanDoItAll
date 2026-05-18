# Assumptions And Risks

## Working Assumptions

- The original v2 bundle is the authoritative product contract unless contradicted by current user instructions.
- LB4U sources are internal project artifacts and may contain personal or sensitive details; execution must minimize unnecessary copying and must mask secrets in logs.
- `routery hesla` is excluded even if technically readable.
- OpenAI `gpt-5-mini` and Ollama `gptoss20b64k` are expected to be available through existing CanDoItAll provider profiles or can be configured through existing provider settings during execution.
- The follow-up can add focused tests and refactors, but should not introduce a new memory architecture unless validation proves the current one cannot be fixed.

## Critical Path Risks

- Model/provider configuration may exist outside the cognitive memory module, making token-limit validation depend on runtime environment setup.
- LB4U file formats span docx, pdf, pptx, xlsx, images, APKs, Eagle files, and firmware archives; execution must separate semantic ingestion from asset-node registration.
- Current consolidation may pass unit tests while failing useful-memory behavior because keyword classification can look functional on synthetic data.
- Refactoring oversized files before behavioral proof could churn the code without improving memory quality.
- Local Ollama output limits may truncate responses silently unless token budgets and response metadata are explicitly captured.

## Validation Risks

- A generic LLM answer can sound good while not being memory-backed; validation must inspect context sources and review items, not only final prose.
- Staged ingestion can accidentally overfit LB4U if test assertions require exact sentences rather than domain-relevant canonical facts.
- Cross-project knowledge is hard to prove with one project; this round can validate candidate extraction and review behavior, but deeper generalization should use more projects later.
- Secret exclusion must be validated by absence from ingestion manifests, source snapshots, prompts, and recall results.
- Qdrant/vector unavailability must be explicit; no silent downgrade can be accepted as a successful semantic validation.

## Reopen Triggers

- Any probe answer includes content from `routery hesla` or other excluded files.
- Any accepted canonical memory lacks raw source provenance.
- OpenAI validation succeeds but Ollama validation truncates or omits required sections without a visible warning.
- Consolidation produces generic business-plan knowledge without traceable support.
- Recall answers become model-only summaries with no source evidence.
- Refactor subbundles leave public API routes, persistence models, or tests broken.
- Prepared or completed bundle validation fails.
