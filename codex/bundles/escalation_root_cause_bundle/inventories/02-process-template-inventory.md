# Process Template Inventory

## Full Scope Counts

- Process definitions: 24.
- Step markdown files: 155.
- Validation JSON files: 30.
- Checklist JSON files: 30.
- Prompt JSON files: 30.
- Role JSON files: 54.
- Shared process files: 70.
- Artifact JSON templates: 6.
- Artifact markdown templates: 6.

## High-Risk Process Definitions

| Process | Source reference | Risk class |
| --- | --- | --- |
| `dotnet-solution-setup` | `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json` | Deterministic scaffold/wire/readback, required tool receipts, repair/escalation branch. |
| `dotnet-development-slice` | `repo://Templates/Processes/processes/dotnet-development-slice/definition.json` | Runtime-owned subprocess parent for solution setup and feature slices. |
| `dotnet-feature-function-implementation` | `repo://Templates/Processes/processes/dotnet-feature-function-implementation/definition.json` | Validation/rework loops and proof receipts. |
| `software-delivery` | `repo://Templates/Processes/processes/software-delivery/definition.json` | Multiple runtime-owned subprocesses, accepted/no-go outputs, screenshot and command writeback children. |
| `dotnet-runtime-command-writeback` | `repo://Templates/Processes/processes/dotnet-runtime-command-writeback/definition.json` | Runtime command writeback, current run context, and produced child output mapping. |
| `dotnet-ui-screenshot-writeback` | `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json` | Screenshot artifact and image-analysis receipt proof. |
| `dotnet-architecture-design-review` | `repo://Templates/Processes/processes/dotnet-architecture-design-review/definition.json` | Subprocess handoff and architecture artifact proof. |
| `blazor-app-delivery` | `repo://Templates/Processes/processes/blazor-app-delivery/definition.json` | Runtime/tool proof and repair/revalidate loop. |
| `blazor-app-repair-fix` | `repo://Templates/Processes/processes/blazor-app-repair-fix/definition.json` | Repair loop and runtime validation proof. |
| `blazor-backend-feature` | `repo://Templates/Processes/processes/blazor-backend-feature/definition.json` | Runtime/tool proof and validation receipts. |
| `blazor-frontend-feature` | `repo://Templates/Processes/processes/blazor-frontend-feature/definition.json` | UI validation, runtime proof, and screenshot risk. |
| `blazor-fullstack-feature` | `repo://Templates/Processes/processes/blazor-fullstack-feature/definition.json` | Cross-layer runtime validation and proof receipts. |
| `app-page-screenshot` | `repo://Templates/Processes/processes/app-page-screenshot/definition.json` | Screenshot and image-analysis tool receipts. |
| `app-pages-screenshot-set` | `repo://Templates/Processes/processes/app-pages-screenshot-set/definition.json` | Multi-page screenshot proof and artifact slots. |

## Audit Requirement

Every process definition, step markdown, validation JSON, prompt JSON, and shared artifact reference must be marked as one of:

- `Migrated`: hard gates are typed and validated.
- `Already typed`: source already has enforceable typed metadata.
- `Prose-only risk removed`: prose remains explanatory only.
- `Explicit exception`: no hard runtime/proof gate exists, with source proof.
- `Blocked`: implementation discovered source facts requiring design escalation.
