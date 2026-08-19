# Coverage and uncertainty

## Directly inspected

The preparation directly inspected the central workspace process host, command environment/executable/process runner, MCP policy/resolver/launcher/environment/Playwright resolver, ExternalProcessToolInvoker, Workbench runtime launcher and direct-dotnet policy, Manager process tools/Tailwind supervisor/project, Docker host tools, FileTools integration, current Processes driver abstractions, and current MAF ownership ADR.

## Search-confirmed but requiring B00 inspection

- main WatchSupervisorService and TuningExecutionAdapter details;
- all runtime-node metadata models/writers/readers;
- every plugin/special tool/process driver that launches a process;
- current tests and process lifetime composition after Core C4;
- package/native behavior on actual hosts.

## Not proven during preparation

- process-tree termination semantics on any target OS;
- macOS process discovery mechanism;
- desktop/terminal behavior;
- FileTools package support;
- Docker daemon/context permutations;
- actual local stdio MCP setup on Linux/macOS;
- current CI runtime behavior.

B00 must update every uncertainty before implementation and apply split triggers.
