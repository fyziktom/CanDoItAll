# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Closure proof | Notes |
| --- | --- | --- | --- | --- |
| Base prompt must not be .NET/calculator-specific. | `requirements/01-normalized-requirements.md#r001` | `subbundles/01-base-process-prompt-flexibility` | Prompt tests passed; source scan found no calculator/Blazor/.NET scaffold terms in the base prompt file. | Critical foundation. |
| Generic process evidence contract must remain. | `architecture/01-target-solution.md` | `subbundles/01-base-process-prompt-flexibility` | Targeted `ProcessRunAutomationDispatchServiceTests` passed. | Guarded evidence and outcome rules remain. |
| .NET-specific knowledge belongs to .NET agents/skills. | `requirements/01-normalized-requirements.md#r003` | `subbundles/02-specialized-default-agent-catalog` | Seed catalog tests and instruction assets passed inspection. | Agent specializations carry technology tactics. |
| Add JS architect/developer/QA defaults. | `requirements/01-normalized-requirements.md#r004` | `subbundles/02-specialized-default-agent-catalog` | Seed catalog tests passed. | JS agents avoid .NET-only capability assumptions. |
| Add business, finance, marketing defaults. | `requirements/01-normalized-requirements.md#r005` | `subbundles/02-specialized-default-agent-catalog` | Seed tests passed and live specialist-agent validation passed. | Non-coding process support. |
| Add default business-plan process scenario. | `requirements/01-normalized-requirements.md#r006` | `subbundles/03-scenario-process-templates-and-validation-harness` | Template pack load/projection tests passed. | Tests business/finance/marketing handoff. |
| Use PostgreSQL for process validation. | `requirements/01-normalized-requirements.md#r008` | `subbundles/04-postgresql-process-validation-proof` | PostgreSQL-backed deterministic process run passed. | SQLite was not used for process validation. |
| Attempt real-agent validation after atomic tests. | `requirements/01-normalized-requirements.md#r009` | `subbundles/04-postgresql-process-validation-proof` | PostgreSQL-backed opt-in live specialist-agent validation passed. | Atomic tests ran before live validation. |
