# SB03 Proof Manifest

- Subbundle id: `SB03-proof-restart-and-browser-demo`
- Status: `Completed`
- Owned requirements: R001, R002, R003, R004, R005
- Raw notes: N001, N002, N003, N004, N005
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.Web/Program.cs` | `69820ab7d2a2a167bbcc37779f414b8661dbd8f11717ee636ab53dc5d7746d4a` | `44b0bcf69bbbaee9a5a4efe9193e4d7386113d42a5bdd0d92171993354d42010` |

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB03/transcripts/failing-first-static-asset-health.txt`
- Passing transcript: `bundle://proof/SB03/transcripts/passing-web-build.txt`
- Passing transcript: `bundle://proof/SB03/transcripts/passing-5032-health.txt`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`

## Source Assertions

- Source proof: `repo://src/CanDoItAll.Web/Program.cs` calls `UseStaticWebAssets()` so the rebuilt app serves component static assets when launched from build output.
- Test name: `CanDoItAll.Web build`
- Test name: `Processes route and voice JS health check`
- Test proof: `bundle://proof/SB03/transcripts/passing-web-build.txt`
- Test proof: `bundle://proof/SB03/transcripts/passing-5032-health.txt`
- Adversarial negative proof: `bundle://proof/SB03/transcripts/failing-first-static-asset-health.txt`
- Anti-stub audit: No production stubs found in `bundle://proof/SB03/transcripts/anti-stub-audit.txt`.

## Browser Or Host Proof

- Browser screenshot: `repo://.artifacts/process-manager-audio-reply-and-run-metrics/final-5032-manager-chat.png`
- Browser snapshot: `repo://.playwright-mcp/page-2026-06-27T22-05-32-968Z.yml`
- Browser console: `repo://.playwright-mcp/console-2026-06-27T22-05-26-649Z.log`
- Host proof: `bundle://proof/SB03/transcripts/passing-5032-health.txt`
