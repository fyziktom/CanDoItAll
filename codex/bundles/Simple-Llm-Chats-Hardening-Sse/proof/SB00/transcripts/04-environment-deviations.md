# Environment and diagnostic deviations

The accepted proof consists only of the two focused `dotnet test` commands in transcripts 02 and 03.
The following diagnostic attempts are not acceptance commands and are not recorded in the proof
manifest command budget:

1. A first detached-development attempt could not read the sandboxed NuGet configuration. It was rerun
   with the same scope after approval.
2. The original temporary worktree path exceeded the Windows legacy path limit during build. The exact
   target was verified inside the repository and replaced with the shorter `.w\d` worktree.
3. A feature build attempt encountered Release assembly locks held by the user-owned
   `CanDoItAll.Web` process 55796. That process was not stopped. Because the baseline merge changed only
   `README.md` and a migration guidance document, the focused feature comparison used the already-built
   Release outputs with `--no-build --no-restore`.

None of these attempts broadened test selection, changed production source, or hid a current
branch-induced failure.
