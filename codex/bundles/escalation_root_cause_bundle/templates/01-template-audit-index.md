# Template Audit Index

## Audit Dimensions

Every process and artifact template must be reviewed against these dimensions:

- Execution class is typed.
- Required runtime tools are typed.
- Required receipts include exact tool name and relevant args/path/scope.
- Product completion paths and readbacks are typed.
- Produced artifact slots are typed.
- Accepted child outputs and no-go child outputs are typed.
- Manual skip cannot bypass required evidence.
- Branch outcomes use enums or typed identifiers, not scattered strings.
- Markdown prose is explanatory, not the only source of a hard gate.

## Required Coverage

- All process definitions under `repo://Templates/Processes/processes`.
- All step markdown files under `repo://Templates/Processes/processes`.
- All validation JSON files under `repo://Templates/Processes/processes`.
- All prompt JSON files under `repo://Templates/Processes/processes`.
- All artifact JSON files under `repo://Templates/Processes/processes/business-plan-development/artifacts`.
- Shared process artifacts and framework metadata under `repo://Templates/Processes/shared` and `repo://Templates/Processes/manifest.json`.

## Output

Implementation must produce a final audit table in `proof/SB09/manifest.md` or a linked proof artifact that lists every source-controlled template file with its disposition.
