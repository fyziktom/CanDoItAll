# Migration and compatibility

## Expand

- Add Usage contract/project and typed producer/selection semantics.
- Add Core/Application target projects.
- Add Runtime and Persistence target projects and explicit registrations.
- Add append-only Simple Chat invocation usage/pricing columns.
- Add reusable Components project.
- Add Agent tab/query catalog and route adapter.

## Migrate

- Move domain types first, then Application ports/use cases as one SB03 cutover.
- Move provider execution out of Persistence in SB04.
- Move EF/data profile implementations under the MAF Persistence project in SB05.
- Retarget API adapters, AppDbContext scanning, migration assembly references, composition, tests, and solution entries.
- Migrate UI consumers to Components and Agent page composition.
- Add no-new-caller guards for old projects/namespaces.

## Contract

- Remove CanDoItAll.Modules.LlmChats, .Persistence, and .Ui projects.
- Remove CanDoItAll.Modules.LlmChats* production namespaces.
- Remove LlmChatsShellNavigationContributor and routed full LlmChatsPage.
- Remove duplicate DI and assembly markers.
- Preserve redirect-only /chats in Modules.AgentFramework with a documented future removal owner.

## EF requirements

- Keep LlmChats_* table names.
- Do not edit historical migration source to rename CLR namespaces.
- Add one append-only migration for new usage/pricing evidence.
- Migration SQL must contain no unintended DropTable/RenameTable for existing Simple Chat tables.
- Model snapshot and has-pending-model-changes checks must be clean at CP1/CP4.
- Backfill is deterministic and idempotent.
- Database transfer export/import includes new fields and preserves old documents.

## HTTP/security requirements

- Route templates, OpenAPI operation behavior, API scopes/policies, SSE replay/cursors, status/error mapping, and profile fences remain stable.
- Namespace changes are internal CLR changes and do not alter serialized contract names.
- Logs never include provider secrets, system prompts, full messages, or raw provider payloads.

