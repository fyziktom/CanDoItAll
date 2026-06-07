# Driver Negative Architecture Guard

## Denied Production Surfaces
- Production process-helper-driver interfaces are denied.
- Driver registry and runtime selector types are denied.
- Driver DI registration is denied.
- Manager commands that select or execute process helper drivers are denied.
- Execution-capable helper drivers are denied.

## Allowed Documentation Surface
- Architecture notes may describe future driver evidence vocabulary.
- Tests may assert that production driver tokens are absent.
- Bundle proof may cite driver-token scans.

## Negative Proof
The production driver token scan must pass before SB030, SB033, and SB036 can close.

Required scan tokens:
- `IProcessDriverPack`
- `IProcessDriverRegistry`
- `ProcessDriverRegistry`
- `IProcessHelperDriver`
- `IProcessSwDevHelperDriver`
- `IProcessDotNetSwDevHelperDriver`
- `MapProcessDriver`
