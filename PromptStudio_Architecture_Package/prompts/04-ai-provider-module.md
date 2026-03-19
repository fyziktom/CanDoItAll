# Codex Prompt 04 — AI Provider Module (OpenAI and Ollama)

## Objective
Implement workspace/provider profile management and the provider abstraction layer for OpenAI and Ollama local/remote.

## Required reading
1. `docs/02-technical-requirements.md`
2. `docs/04-solution-architecture.md`
3. `docs/07-implementation-plan.md`
4. `docs/09-validation-and-testing-plan.md`
5. `docs/11-references.md`

## Constraints
- Use .NET 10 and C#.
- Use Blazor Web App with Interactive Server rendering.
- Use Tailwind CSS and the shared component strategy.
- Keep code comments in English.
- Preserve the modular monolith boundaries from the architecture package.
- Prefer one `DbContext` per operation via `IDbContextFactory`.
- Keep business logic out of page-only code.
- Do not log or expose secrets.
- Add or update tests for the touched behavior.
- Keep naming and file structure aligned with the package documents.

## Scope
This prompt covers provider profiles, provider abstraction, health checks, capability flags, and safe settings flows.

## Tasks
1. Implement the Workspace module pieces for provider profiles and workspace defaults.
2. Add CRUD UI for OpenAI and Ollama profiles.
3. Support secret references for provider credentials.
4. Implement provider abstractions such as registry, health service, and execution facade.
5. Add capability flags to the provider profile model or capability resolver.
6. Implement health check/test-connection flows.
7. Keep provider-specific behavior behind adapter boundaries.
8. Add tests for provider profile validation, health status handling, and secure settings persistence.

## Required deliverables
- provider profile domain and UI
- OpenAI profile support
- Ollama local profile support
- Ollama remote profile support
- provider abstraction/services
- health checks and tests

## Acceptance criteria
- a user can create and edit provider profiles from Settings
- secret references are used for credentials
- provider health checks surface useful status
- provider abstractions are usable by later prompt-generation flows
- no provider-specific details leak into unrelated modules
- tests cover profile validation and health logic

## Session output format
1. Scope summary
2. Implementation plan
3. Changed files
4. Test/build commands
5. Completion summary
6. Follow-up risks or next steps

## Stop condition
Stop when provider settings are production-shaped for local use and ready for the prompt factory.