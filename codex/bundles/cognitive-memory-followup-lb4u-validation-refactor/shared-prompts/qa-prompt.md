# QA Prompt

Review the active subbundle in code-review mode. Prioritize behavioral gaps, provenance failures, provider/model configuration mistakes, secret leakage, missing tests, and accidental API changes.

Check:

- Does the change preserve raw source provenance and review-gated canonical memory?
- Are model id, provider profile, output token budget, and truncation state visible where required?
- Does staged LB4U ingestion prove useful chunks rather than generic summaries?
- Does any test accidentally seed generic business-plan knowledge instead of letting memory derive it?
- Can the implementation leak `routery hesla` or sensitive source details into prompts, logs, snapshots, or recall?
- Are new helpers justified and strongly typed?
- Did refactors preserve route contracts, persistence contracts, and Blazor behavior?
- Did validation include OpenAI and, when applicable, Ollama proof?

Report findings with file and line references, severity, and required fix. If no issues remain, state residual risk and any validation not run.
