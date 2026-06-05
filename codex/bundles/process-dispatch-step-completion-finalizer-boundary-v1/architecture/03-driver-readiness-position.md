# Driver Readiness Position

Driver preparation is relevant, but production drivers are still premature.

## What may be prepared now

Documentation-only map from finalizer/evidence semantics to future process helper drivers:

- artifact producer kinds,
- artifact expectation modes,
- validation statuses,
- failure ownership,
- runtime invariant categories,
- future evidence families such as build/test/browser/document/spreadsheet/business-analysis evidence.

## What must wait

Do not create:

- `IProcessDriverPack`,
- driver registration,
- manager verification driver APIs,
- DotNet/Rust/Office/BusinessAnalysis driver implementations,
- cross-module process driver contracts.

## Reason

The finalizer still owns the truth about how evidence becomes a completed, blocked, failed, or recovered step. Driver APIs should not be designed until this vocabulary is stable and local helper boundaries are proven.
