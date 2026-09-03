# Pattern Decisions

| Decision | Selected approach | Reason | Rejected alternative and cost |
|---|---|---|---|
| SDK argument/result adaptation | Adapter at existing MAF boundary | One translation point for all provider clients | Per-provider business branches would diverge and duplicate safety policy. |
| Completion/recovery assessment | Small concrete policy using typed inputs | Deterministic tests without provider/network/Blazor | Another manager interface or prose classifier adds indirection without authority. |
| Prior outcome context | Bounded projection from canonical records | Preserves application scope independently of SDK session storage | Restoring raw SDK session also restores stale approvals/history and couples provider formats. |
| Side effects and observation errors | Explicit outcome plus effect state | Separates an operation's commit from later diagnostics failure | Catch-and-ignore hides an operational failure; blind retry can create duplicates. |
| UI invalidation | Existing scoped observer/notification hub | Canvas already knows how to reread canonical state | New global event bus or direct runtime-to-component calls enlarge scope and weaken isolation. |
| Tool families | Targeted asset collaborator | Actual managed-storage responsibility and test seam | Whole-catalog strategy framework or partial-file split is not necessary for this incident. |
| Shared/direct endpoints | Existing SDK/relay adapters | Both already enter the same local agent loop | New provider abstraction, migration or SDK upgrade is unsupported by current evidence. |

Use early returns and small functions. Keep strongly typed codes and side-effect modes; use established external-protocol strings only at adapters. No new XML documentation. Rare code comments must be English. Follow the repository's cuddled braces and one statement per line.

These records are design intent. At each closure record the actual types/files and explain any deviation; never mark a pattern implemented solely because a class was created.
