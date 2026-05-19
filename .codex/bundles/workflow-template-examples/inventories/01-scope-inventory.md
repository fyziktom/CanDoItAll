# Scope Inventory

| Area | File or module | Planned change |
| --- | --- | --- |
| Workflow template manifest | `C:\repositories\CanDoItAll\Templates\Workflows\manifest.yaml` | Add new workflow file references and bump seed version. |
| Existing default templates | `C:\repositories\CanDoItAll\Templates\Workflows\workflows\default-workflows.yaml` | Preserve existing keys; no required edit unless validation requires it. |
| Email task templates | `C:\repositories\CanDoItAll\Templates\Workflows\workflows\email-plugin-task-workflows.yaml` | New data file with Gmail and Office365 task examples. |
| File-analysis templates | `C:\repositories\CanDoItAll\Templates\Workflows\workflows\file-analysis-workflows.yaml` | New data file with Mermaid and source-code examples. |
| Unit tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs` or a nearby workflow catalog test | Add/extend tests for loading new template keys and compiling graphs. |
| Out of scope | Plugin OAuth runtime and web UI layout | No functional code changes unless template validation reveals mismatch. |
