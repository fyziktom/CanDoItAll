# Supplied bundle disposition

The uploaded ZIP is the analytical predecessor of this program.

## Preserved assets

The following concepts were retained and strengthened:

- portable/materialized/completed validation stages;
- source anchor and source-reference manifest;
- generated portability scanner;
- baseline PowerShell and shell entry points;
- phase gates and conditional correction/recovery paths;
- requirement traceability;
- external compatibility, migration, process-ownership, CI, and browser evidence templates;
- strict redaction and fail-safe rules.

## Reworked assets

| Old area | New disposition |
|---|---|
| Linux-only title and target | Expanded to Linux and macOS with Windows regression |
| Platform/path/composition in one early subbundle | Split: path/config first, filesystem next, composition only after storage/secrets |
| Secrets before full filesystem/storage foundation | Moved after atomicity, modes, root, and storage migration prerequisites |
| Runtime/Manager inside the same implementation bundle | Moved to a second bundle blocked by Core C4 |
| MCP/tools mixed before core hosting proof | Moved after shared process primitives and Manager ownership |
| Generic platform driver language | Replaced by purpose-owned ports and leaf adapters |
| Linux `/proc` suggestion as enough process ownership | Registry-first ownership with bounded Windows/Linux/macOS recovery adapters |
| FileTools Linux claim based on package metadata | Replaced by an explicit pinned compatibility report |
| CI extension | Replaced by active workflow restoration because current CI is disabled |
| Linux-only final gate | Replaced by Windows/Ubuntu/macOS core and runtime gates |

## Superseded files

The old subbundle IDs `SB01`–`SB09`, `SB90`, and `SB91` must not be executed against the current branch. Their useful requirements are represented in `A00`–`A92` and `B00`–`B91`.

The old bundle remains useful as provenance but is not embedded in this ZIP to avoid two executable plans with conflicting ordering.
