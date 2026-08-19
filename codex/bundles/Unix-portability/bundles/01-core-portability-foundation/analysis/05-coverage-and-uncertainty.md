# Coverage and uncertainty

## Directly inspected

The preparation directly inspected the current solution/project graph, development configuration, control-plane path and profile services, storage path/driver code, Security vault/model/DI code, Data Protection registration, MAF path policies, FileTools integration, current CI file location, and the latest MAF process-ownership ADR.

## Search-confirmed but requiring A00 inspection

- runtime secret resolver/brokers;
- selected tests and documentation paths;
- additional persistence fields discovered by generated scan;
- package/native behavior that cannot be proven from source alone.

## Not proven during preparation

- local restore/build/test result;
- actual Linux/macOS filesystem and permissions behavior;
- native Keychain/Secret Service implementation choices;
- PostgreSQL installation/service behavior on macOS;
- actual FileTools package behavior;
- CI workflow protection settings.

No unproven item may be converted into a support claim. A00 must update this file with local evidence.
