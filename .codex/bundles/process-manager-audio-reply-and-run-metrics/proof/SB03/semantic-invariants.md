# SB03 Semantic Invariants

## Invariant SB03-5032-RESTART

- Invariant ID: `SB03-5032-RESTART`
- Source raw note: Final closure for N001, N002, N003, N004, N005
- Expected behavior: The rebuilt 5032 instance must serve the Processes page and the voice JavaScript asset, and the Manager chat tab must load with selected run, manager agent, prompt context, and voice controls visible.
- Disallowed shallow implementation: A build that succeeds but leaves the old process running, or a page that returns HTTP 200 while component voice assets fail, is insufficient.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first-static-asset-health.txt`
- Passing test: `bundle://proof/SB03/transcripts/passing-web-build.txt`; `bundle://proof/SB03/transcripts/passing-5032-health.txt`
- Changed source files: `repo://src/CanDoItAll.Web/Program.cs` before `69820ab7d2a2a167bbcc37779f414b8661dbd8f11717ee636ab53dc5d7746d4a` after `44b0bcf69bbbaee9a5a4efe9193e4d7386113d42a5bdd0d92171993354d42010`.
- Production assertions: `repo://src/CanDoItAll.Web/Program.cs` invokes `UseStaticWebAssets()` before building the app, and `bundle://proof/SB03/transcripts/passing-5032-health.txt` shows HTTP 200 for the Processes route and voice JS asset on PID 10264.
- Red-team negative case: `bundle://proof/SB03/transcripts/failing-first-static-asset-health.txt` rejects the old static asset failure condition by failing when the rebuilt 5032 host serves the voice JS asset.
- Downstream dependency check: `repo://.artifacts/process-manager-audio-reply-and-run-metrics/final-5032-manager-chat.png` shows the Manager tab loaded after restart.
