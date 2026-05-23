# Structured Input

## Raw Notes

| Note id | Exact wording | Normalized intent |
| --- | --- | --- |
| `N001` | "we have troubles during lots of other things because of cognitive memory" | Cognitive Memory must not break unrelated agent, workflow, or demo flows when disabled. |
| `N002` | "Agent context contributor 'cognitive-memory.context' reported failure: Cognitive Memory context requires a project scope." | The agent context contributor must skip before project-scope resolution when runtime usage is disabled. |
| `N003` | "add some global setting, that cognitive memory is not used" | Add a persisted global usage flag, separate from provider/model policy. |
| `N004` | "possible to turn it on/off during runtime" | Use database-backed settings and existing settings UI/API instead of startup-only configuration. |
| `N005` | "add it into all places where it is kind of connected to other parts" | Gate optional integration points that inject/run Cognitive Memory from general agent, workflow, or scheduled automation paths. |
| `N006` | "if it is disabled it must skip those steps" | Disabled behavior is explicit skip/no-op, not failure and not hidden fallback while enabled. |
| `N007` | "setup for me clean development db" | Reset and migrate `candoitall_development` PostgreSQL after implementation. |

## Assumptions

- "Global" means persisted application setting stored with Cognitive Memory automation settings.
- Direct status/settings/database endpoints must remain usable while disabled so the user can turn Cognitive Memory back on.
- Existing `CognitiveMemoryModelAccessMode.Disabled` is provider-policy scoped and is not sufficient because required context modes can still fail; the new flag must skip before policy/project-scope checks.

## Validation Expectations

- Unit tests cover disabled agent context skip and disabled scheduled automation skip.
- Settings tests cover persistence of the new flag.
- Component/API shape compiles with the new setting.
- Development PostgreSQL is reset, migrated, and left ready for manual testing.
