# Structured Input

## Normalized Problem

`MafAgentRuntime` is still a large partial-class runtime that hides many implementation classes and DTOs as private nested types. This makes architecture hard to read, forces tests to reach through the public runtime or reflection/static helpers, and keeps responsibilities coupled to an outer runtime instance.

## Raw Notes

| Note | Literal Input | Normalized Meaning |
| --- | --- | --- |
| N001 | "this was just very small part of the isolation" | The previous extraction is insufficient and must not be treated as complete. |
| N002 | "Why are those builders and other parts inside partial class of MafAgentRuntime?" | Builder classes and helper types must be extracted from private nested runtime scope. |
| N003 | "It needs correct isolation" | Extract responsibilities into named runtime services/drivers with explicit contracts. |
| N004 | "plenty of the classes that are kind of hidden under that MafAgentRuntime" | Inventory all nested and partial-owned hidden classes before implementation. |
| N005 | "very hard to understand the structure of the code" | Target architecture must make file/type ownership discoverable from the folder and type names. |
| N006 | "more maintainable and isolated for better unit testing" | Direct tests must target extracted collaborators without constructing full `MafAgentRuntime` unless testing orchestration. |
| N007 | "avoid to add everything under mafagentruntime" | Do not create more `MafAgentRuntime.*.cs` partials as the main solution. |
| N008 | "prepare new bundle... do not implement it yet" | This turn must produce planning artifacts only. |

## Constraints

- No production implementation in this preparation turn.
- No domain-specific agent/tool feature work.
- Preserve current runtime behavior during future implementation.
- Do not replace partial-class coupling with a service-locator or "manager" class of similar size.
- Avoid unnecessary public API expansion; prefer `internal` collaborators unless tests require `InternalsVisibleTo`.

## Expected Validation

- Bundle validates at prepared stage.
- Future implementation requires build, focused unit tests, direct collaborator tests, MAF handoff/runtime smoke tests, architecture guard tests, and performance/startup measurements.
