# Structured Input

## Raw Notes

| Note | Exact wording | Normalized meaning | Owning subbundle |
| --- | --- | --- | --- |
| `N001` | `do refactoring of each page in our app` | Inventory every routable app page and page-owned large component; implement refactors where length, helper density, or component coupling makes the page hard to maintain. | `09-remaining-route-page-cleanup`, plus targeted earlier subbundles |
| `N002` | `Lots of them are very long and it will be better to split them into own smaller components.` | Split long markup regions into focused components with typed parameters, callbacks, and explicit state ownership. | component subbundles `02`, `04`, `08`, `09` |
| `N003` | `Some pages also contains lots of logic that can be isolated to helpes.` | Move pure or mostly pure page helper logic into strongly typed helper classes before component extraction. | helper subbundles `01`, `03`, `05`, `06`, `07` |
| `N004` | `project structure page there are lots of helpers for nodes... isolate them into some ProjectStructureNodeHelpers.` | Create a `ProjectStructureNodeHelpers` extraction for node labels, attachment preview decisions, display text, marker or priority labels, and other pure node helpers currently inside `ProjectStructurePage.razor`. | `01-project-structure-node-helpers` |
| `N005` | `best to create detailed checklist with references in xlsx` | Create a workbook checklist with file references, route references, refactor type, priority, planned subbundle, events/state risks, and validation commands. | preparation artifact |
| `N006` | `create subbundle for each change and do them atomically. first isolations of helpers... then components isolations.` | Execute helper extraction subbundles before component extraction subbundles; each subbundle must own a coherent file set and have its own gate. | all subbundles |
| `N007` | `preserve all functionality and test it that all works as before` | Run targeted component/unit tests, build, and browser proof for changed routes before closure. | `10-final-regression-proof-and-closure` |

## Hard Constraints

- Use `candoitall-bundle-workflow` as the durable workflow.
- Do not touch feature code until the prepared-stage bundle validator passes.
- Keep existing public route behavior and test ids stable unless a subbundle explicitly says otherwise.
- Keep page-to-service orchestration in pages unless logic is pure helper logic or a real boundary.
- Use existing BaseLib, CanvasLib, and module components for layout and UI extraction.
