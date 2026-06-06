# No-Core Cutline

This bundle must not start Process Core extraction.

Allowed:
- internal top-level classes under `CanDoItAll.Modules.Processes`;
- internal interfaces whose name is not `IProcessDriver*` and whose scope is only artifact projection;
- module-local context, host and coordinator classes;
- source scans and documentation-only driver-readiness maps.

Forbidden:
- `src/CanDoItAll.Processes.Core`;
- `CanDoItAll.Processes.Core` namespace;
- `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, `ProcessDriverPack`;
- public projection APIs consumed outside `CanDoItAll.Modules.Processes`;
- moving EF entities or persistence mapping;
- UI changes.
