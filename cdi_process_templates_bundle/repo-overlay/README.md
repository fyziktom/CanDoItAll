# Repo overlay

This folder is designed to be copied into the root of the CanDoItAll repository.

## Intent
- add a file-driven process template pack under `output/process-template-pack`
- keep process definitions, sidecars, and workbook-driven source material outside hardcoded C# logic
- align the template pack to the current module architecture with explicit dependencies, artifact inputs, and decision roles
- add regression tests for current baseline expectations
- keep the remaining definition-canvas chrome hardcode visible through a dedicated corrective subbundle

## Apply sequence
1. Copy `repo-overlay/Directory.Build.targets` into the repository root.
2. Copy `repo-overlay/src/...` into the repository `src/...` tree.
3. Copy `repo-overlay/tests/...` into the repository `tests/...` tree.
4. Run `tools/validate_process_template_pack.py`.
5. Run build and test in an environment that has `dotnet` SDK available.
6. Execute the architecture review gates and corrective-subbundle rules from the root docs before calling the work complete.

## Current architecture note
The overlay still uses projected import envelopes as the compatibility boundary for the current module, but the authored source of truth remains the folder-based template pack plus workbook and sidecar markdown files.
