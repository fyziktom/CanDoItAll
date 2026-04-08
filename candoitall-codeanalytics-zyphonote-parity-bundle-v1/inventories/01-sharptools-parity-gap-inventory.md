# SharpTools Parity Gap Inventory

## Already covered in the current CodeAnalytics MCP

| SharpTools capability | Current CodeAnalytics answer |
| --- | --- |
| `SearchDefinitions` | `code_analytics_symbols_search` |
| `ViewDefinition` | `code_analytics_symbol_definition_get` |
| `GetMembers` | `code_analytics_symbol_members_get` |
| `ListImplementations` | `code_analytics_symbol_implementations_get` |
| `FindReferences` | `code_analytics_symbol_references_get` |

## Still missing or still weaker for the Zyphonote parity target

| Gap | Why it still matters | Planned answer in this bundle |
| --- | --- | --- |
| Direct project-reference surface | Scenario 1 needs clean direct references, not usage-weighted dependency edges | Add project and solution inventory / reference tools |
| Project-level entry point comparable to `LoadProject` | Architecture work still benefits from an explicit project-centered view | Expose project metadata, direct references, packages, and document inventory |
| Raw source inspection comparable to `ReadRawFromRoslynDocument` | Scenario 4 and broader code reading still fall back to shell file reads too quickly | Add a snapshot-backed document/source tool |
| File/type tree inspection comparable to `ReadTypesFromRoslynDocument` | Agents still need a quick file-to-types bridge without manual shell reads | Add a document types tool or equivalent file-contained symbol listing |
| Reliable member-behavior path | Focused context failed on a real method question | Fix focused context for member seeds or add a deterministic behavior-oriented inspection tool |

## Explicit non-goals for this pass

- Do not attempt SharpTools editing parity in this code-analysis MCP.
- Do not add a second fully separate query engine when snapshot-backed data plus source access is enough.
- Do not change the Zyphonote scenario set to make the rerun easier.
