# QA / Red-Team Prompt

Check that the implementation did not merely satisfy status rows.

Verify:
- production code changed as claimed,
- no stubs/TODO/NotImplemented markers,
- no runtime host/registry/selector/DI/manager command,
- no file/network/workspace/storage/process mutation in verifier packages or adapter,
- transcript verifier decomposition preserves existing diagnostics,
- runtime evidence verifier detects real contradictory descriptors,
- audit/redaction/no-mutation are present on every response,
- source scans and tests are attached as transcripts,
- final bundle validators pass.
