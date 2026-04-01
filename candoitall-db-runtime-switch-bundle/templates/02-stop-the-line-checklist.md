# Stop-The-Line Checklist

Do **not** mark a subbundle complete if any item below is still true.

- A required `dotnet test` command was not run and the subbundle was still marked complete.
- A UI-changing subbundle has no browser screenshots or no screenshot review notes.
- The execution report still contains placeholder/pending language for the subbundle's own proof.
- Runtime database switching was claimed but the app still requires a process restart.
- The active database is not clearly visible in the UI, but the UX subbundle was marked complete.
- `EnsureCreatedAsync()` still acts as the normal production schema path after the migrations subbundle claims completion.
- `/managed-files` still points to one fixed startup file provider after the storage subbundle claims completion.
- The browser workbench key is still global after the workbench-isolation subbundle claims completion.
- Stale artifact routes still crash or show the Blazor error UI after a database switch.
- PostgreSQL support was claimed without real PostgreSQL automated proof.
- Clone/snapshot support was claimed without verifying profile-scoped storage files.
- IPFS support was claimed without either fake-server automated proof or real-node proof.
- A blocked environment dependency was rewritten as a success instead of `Blocked`.
- A later subbundle exposed that an earlier critical foundation was weak, but the earlier subbundle was not reopened.
