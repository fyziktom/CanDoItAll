# Requirements Decomposition

## User Need To Architecture Work

| User need | Architecture implication | Bundle phase |
| --- | --- | --- |
| Avoid domain leaks in MAF wrapper | Common workspace plugin must be domain-neutral and development prompts must move out | SB01, SB05 |
| Add specific instructions through processes | Process templates need scoped instruction fragments separate from common MAF tools | SB03, SB04 |
| Limit tools, skills, and MCPs | Process step scope must compile into MAF deny/require policies | SB02, SB03, SB04 |
| Force a tool or instruction carrier | Required capability declarations must be supported and fail if missing or denied | SB02, SB04 |
| Suppress development skill for management-only step | Skills must be filtered before context assembly and shown as excluded in manifest | SB02, SB06 |
| Refactor in phases | MAF foundation first, process contracts second, integration third, domain migration fourth, proof last | All subbundles |

## Execution Split

- SB01 removes or isolates the immediate common MAF domain leak.
- SB02 repairs the MAF capability suppression/requirement mechanism.
- SB03 gives processes a typed way to express per-step scope and instructions.
- SB04 connects process scope to MAF metadata and runtime context.
- SB05 moves development-specific image analysis into the correct owner.
- SB06 proves the system together and runs architecture gates.
