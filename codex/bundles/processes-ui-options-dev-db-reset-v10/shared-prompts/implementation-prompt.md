# Implementation Prompt

Execute this bundle one subbundle at a time. Preserve the raw request exactly: add missing role and process-step UI options, then clear and reload only process data in the development database while keeping agents, plugins, memory, projects, project structure, and related files intact.

For SB01, start from the template vocabulary audit and implement the smallest strongly typed change set. Do not add stringly typed one-off UI conditions. Use existing Blazor shared components and current form patterns. Add tests that fail if current template vocabulary cannot map to supported UI/domain options.

For SB02, do not drop the database and do not truncate non-process tables. Generate and review the SQL process table target list first, then execute only against `Processes_%` tables in `candoitall_development`. Capture before/after counts for representative non-process tables and reload current templates after the process reset.
