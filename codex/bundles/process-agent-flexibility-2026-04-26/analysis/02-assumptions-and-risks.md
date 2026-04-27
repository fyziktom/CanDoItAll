# Assumptions And Risks

## Assumptions

- The correct architecture is to keep the process dispatcher responsible for generic process contracts and move technology/domain tactics into seeded agent instructions, capabilities, and process templates.
- Existing calculator-focused implementation proof logic can be relaxed in the prompt without removing generic validation gates.
- New managed seed agents should be refreshed through the existing managed-seed version mechanism rather than through a new migration system.
- JavaScript specialization can start with instructions and existing workspace/provider capabilities; a dedicated JS skill can be added later if the repo already has a skill source or if validation shows the gap.

## Critical Path Risks

- Subbundle 01 is the critical foundation. If .NET/calculator guidance remains in the base prompt, later default agents and non-code process tests will still inherit the wrong behavior.
- Subbundle 02 depends on seed-normalizer refresh lists. If updated agents are not treated as managed seeds, existing workspaces may not receive the improved default instructions.
- Subbundle 03 depends on template-pack compatibility. Invalid JSON or missing manifest entries can break process-template loading and baseline seeding.
- Subbundle 04 depends on local PostgreSQL availability and provider credentials for real-agent runs.

## Validation Risks

- Real-agent validation may be blocked when no usable provider credential is configured. In that case, mock-agent and prompt-shape tests must still prove deterministic behavior, and the real-agent blocker must be explicit.
- Prompt tests can become brittle if they assert large literal strings. Prefer focused presence/absence assertions for base neutrality and specialized-agent content.
- Template-pack tests must validate load/projection, not only file existence.
- PostgreSQL tests can fail from environment setup, port conflicts, or unavailable service. Use existing `PostgresTestAvailability` support and record exact availability results.

## Reopen Triggers

- Reopen subbundle 01 if any generic process prompt still references calculator, `CalculatorEngine`, `Home.razor`, Blazor Web App scaffolding, or `workspace_dotnet_new` outside a clearly technology-specific path.
- Reopen subbundle 02 if any new managed seed agent is missing from fallback or normalizer managed-template-key lists.
- Reopen subbundle 03 if the new process template does not load from `ProcessTemplatePackLoader`, cannot project an import envelope, or lacks artifact expectations for handoff proof.
- Reopen subbundle 04 if only SQLite or in-memory validation ran for process execution.
