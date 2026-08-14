# macOS post-merge handoff

This handoff is intentionally executed after the Unix adoption candidate is
merged into `development`.

## Required anchor

Record the exact `development` commit containing the merge.

## Suggested actual-host checks

- clean package-mode Release build;
- runtime portability Unit and Integration catalogs;
- PostgreSQL migration and restart;
- two headless application start/stop cycles;
- LocalUserFile vault persistence and Unix modes;
- executable/path case and symlink behavior;
- Unix process-group orphan-child cleanup;
- MCP peer-ping and framing tests;
- Docker build/smoke when Docker is available;
- launchd configuration lint/render;
- artifact redaction scan.

## Result

A macOS failure reopens only the affected portability boundary. It does not
retroactively invalidate unrelated Windows/Linux evidence.
