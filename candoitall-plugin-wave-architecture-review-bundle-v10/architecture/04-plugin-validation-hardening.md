# Plugin validation hardening

## Why this is part of phase10
The next feature wave is explicitly plugin-heavy. The shared connector editor now exists, but the repo still needs proof that a brand new plugin manifest can flow through that editor without page-specific code changes.

## Required proof
At least one unknown provider manifest and one unknown resource manifest must be exercised through tests using:
- `Text`,
- `Url`,
- `Number`,
- `Boolean`,
- `Json`,
- `SecretReference`

field types.

The tests must prove that the shared editor and `ConnectorConfigState` can:
- render the fields,
- accept edits,
- serialize/save the data,
- load and round-trip the data back into the editor.

## Why built-in-plugin tests are not enough
Current built-ins prove the direction is correct, but they do not prove future plugin readiness because they still reflect today's known manifests.
