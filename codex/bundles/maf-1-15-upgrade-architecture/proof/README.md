# Proof Structure

Codex must create one proof directory per subbundle:

```text
proof/
  SB01/
  SB02/
  SB03/
  SB04/
  SB05/
  SB06/
  SB07/
  SB08/
```

Recommended contents:

```text
commands/
logs/
binlogs/
trx/
fixtures/
hashes/
package-graph/
telemetry/
source-assertions.md
decision.md
```

Rules:

- Every command has its exact working directory, arguments, exit code, and timestamp.
- Fixtures are sanitized and hash-indexed.
- No credentials, access tokens, raw secrets, unrestricted prompts, or sensitive attachment payloads are committed.
- A gate decision names the reviewed proof and any accepted exceptions.
- “Passed” is not evidence without the command/result artifact.
