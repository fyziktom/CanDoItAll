# Implementation Prompt

Implement `bundle://subbundles/01-project-rename-and-reference-repair` only.

Before editing, confirm the prepared-stage validator passes and the old project still exists at `repo://src/CanDoItAll.Components/CanDoItAll.Components.csproj`. Rename the project to `CanDoItAll.AppComponents`, repair solution/project references and exact facade namespace imports, and keep `CanDoItAll.Components.*` package namespaces intact. Do not edit the sibling `C:\repositories\CanDoItAll.Components` repository.

Capture proof under `bundle://proof/SB01/`, update `bundle://reviews/01-execution-report.md`, and stop if the targeted build, component tests, stale-reference search, or anti-stub audit cannot honestly pass.
