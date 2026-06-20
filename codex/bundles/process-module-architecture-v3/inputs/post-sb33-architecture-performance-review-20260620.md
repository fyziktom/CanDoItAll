# Post-SB33 Architecture And Performance Review Request

Captured on 2026-06-20.

The user asked to use `candoitall-bundle-workflow`, `analyzing-dotnet-performance`, `optimizing-dotnet-performance`, `optimizing-ef-core-queries`, and related skills to review the Process implementation after roughly ten recent improvement commits. The user reported that Process e2e is passing, cleared the `TetrisGame` output folder and project-structure data from the old run, and requested architectural and performance improvements before rerunning the process end to end.

Literal scope:

- Review the prepared bundle analytics and architecture before creating the plan or subbundles.
- Analyze actual architecture and implementation, not only documentation.
- Identify weak parts where processes can get stuck on unexpected or uncovered situations.
- Identify bottlenecks and performance risks.
- Identify code-quality issues, missing shared helpers, enums, and isolated helper areas.
- Analyze too-large or over-responsible files and design better composition for maintainability and testability.
- Keep the process runtime and dispatcher generic.
- If domain-specific logic appears in generic runtime/dispatcher parts, isolate it through domain drivers or strategies.
- Prefer domain-specific drivers/strategies in their own projects, similar to plugins, so future domain drivers can be added easily.
- After repairs, run the `TetrisGame` process e2e again and verify that everything still works.
