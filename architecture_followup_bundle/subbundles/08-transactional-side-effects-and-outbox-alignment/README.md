# Transactional side effects and outbox alignment

## Purpose

Move Process-module search/activity side effects behind a durable boundary so command success does not depend on fragile post-commit best effort.

## Required deliverables
- A durable side-effect boundary for Process save/publish/delete/start-run flows.
- Idempotent or safely retryable dispatch behavior for activity and search projection work.
- Tests proving that forced side-effect failure after DB commit does not corrupt command semantics.
- A short design note explaining whether an existing repository outbox pattern was reused or adapted.

## Repository touchpoints
- `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Publication.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs`
- `src/CanDoItAll.Modules.Automation`
- `src/CanDoItAll.Modules.Workspace/ConnectorOutboxService.cs`
- `tests/CanDoItAll.Tests.Integration`

## Validation commands
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessOutbox" -v:minimal`

## Review questions
1. Do Process commands now remain semantically correct if the external activity/search dispatch fails after commit?
2. Is side-effect dispatch durable or safely retryable instead of best-effort fire-and-forget?
3. Was an existing outbox pattern reused where appropriate rather than inventing a one-off mechanism?

## Corrective trigger

If activity/search dispatch is still a direct post-commit call path, fail and open the side-effects corrective playbook.

## Corrective template

- `subbundles/_corrective-side-effects-reset`

## Detailed execution notes

At minimum, cover these flows:
- definition save
- definition publish
- definition delete
- run start

Do not close this subbundle while a command can still return failure solely because a post-commit side effect threw after the DB state was already durable.
