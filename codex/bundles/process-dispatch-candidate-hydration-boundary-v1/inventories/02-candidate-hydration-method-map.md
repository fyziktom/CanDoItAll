# Candidate Hydration Method Map

Codex must update this inventory from live source in SB02 before production movement.

Expected method/region candidates:

| Method / region | Source file | Category | Side effects | Planned treatment |
| --- | --- | --- | --- | --- |
| `LoadDispatchCandidateHeadersAsync` | Dispatch.cs | Candidate header selection | EF read only | Move to module-local selector/query helper. |
| `LoadDispatchCandidateAsync` run/definition reads | Dispatch.cs | Hydration read model | EF read only | Move to hydration loader snapshot. |
| work brief / all step runs / artifacts / assignments / definitions reads | Dispatch.cs | Hydration read model | EF read only | Move to hydration loader snapshot. |
| branch outcomes / conditional dependency shaping | Dispatch.cs | Candidate shaping | none after read | Move to assembler helper. |
| artifact-input preparation | Dispatch.cs plus existing artifact-input helpers | Prompt shaping | file/path normalization only | Move to artifact input assembler or helper wrapper. |
| subprocess candidate construction | Dispatch.cs | Candidate branch construction | none | Move to candidate factory/assembler. |
| workflow assignment detection | Dispatch.cs | Assignment route shaping | none | Move to assignment resolver helper. |
| direct-agent execution-run recovery selection | Dispatch.cs + Concurrency.cs | Candidate execution facts | execution-client read | Keep client call explicit; helper can consume returned records. |
| technical-agent binding and project-structure read access mutation | Dispatch.cs | Side-effectful binding coordinator | bridge read, editor read/write | Move to explicitly side-effectful coordinator, not pure helper. |
| manual recovery directive loading | Dispatch.cs | Journal read | EF read only | Move to local query helper after candidate facts stable. |
