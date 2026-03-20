# Refactor TODO: `WordToPdfConverter` Testability

## Problem
`Helpers/WordToPDFConverter.cs` directly executes an external LibreOffice process (`Process.Start`) and directly uses static filesystem APIs.  
This makes deterministic unit testing difficult because:
- tests require LibreOffice availability and stable CLI behavior,
- tests depend on real files and machine-specific paths,
- failures are observable only through logging side effects.

## Suggested seam design
1. Introduce an `IProcessRunner` abstraction:
- Method example: `ProcessRunResult Run(ProcessStartInfo startInfo, TimeSpan timeout)`.
- Return code/stdout/stderr should be captured in the result object.

2. Introduce an `IFileSystem` abstraction (or minimal local adapter):
- Methods for `File.Exists`, `Path` operations, and output path checks.
- Keep abstraction narrow to avoid broad rewrite.

3. Convert `WordToPdfConverter` into an instance service:
- Constructor dependencies: `IProcessRunner`, `IFileSystem`, logger.
- Keep current static wrapper method as compatibility shim if needed.

4. Add clear result contract:
- Return per-file conversion results (`Success`, `SkippedMissingInput`, `Failed`, diagnostics).
- Tests can then assert behavior without parsing logs.

## Files likely touched
- `PVEInvoicing/PVEInvoicing/Helpers/WordToPDFConverter.cs`
- `PVEInvoicing/PVEInvoicing/Extensions/ServiceCollectionExtensionsApp.cs` (DI registration)
- New files under `PVEInvoicing/PVEInvoicing/Helpers/Abstractions/` for process/filesystem adapters.

## Tests unlocked after refactor
- Missing input file path returns `SkippedMissingInput`.
- Non-zero process exit maps to deterministic `Failed` result with stderr.
- Successful process execution returns `Success` and output path.
- Timeout/cancellation behavior can be simulated without external processes.
