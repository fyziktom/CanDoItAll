# SB04 Proof Manifest

- Subbundle: `SB04 Browser voice mode demo and closure`
- Status: `Completed`
- Owned requirements: R005, R006 final closure
- Owned raw notes: N006 and final closure for N001 through N005
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 | Notes |
| --- | --- | --- | --- |
| `bundle://proof/SB04/transcripts/playwright-manager-chat-voice.txt` | `new` | `160FFD2A88668FF1BF65D1EC557233116132B6287C20CCAC5C954F673CEE0597` | Live browser transcript with DOM assertions. |
| `bundle://proof/SB04/browser/processes-manager-chat-voice-desktop.png` | `new` | `9F4650C9BCCAF9556409E0BA7D480D42BE598D602788B494D138372E648912E7` | Desktop screenshot after audio mode toggle. |
| `bundle://proof/SB04/transcripts/final-test-run.txt` | `new` | `29FCE1AF1C02F312DA092AB11F56117876DB21F81D8DA38A684FEE3790311ABE` | Final affected test slices. |
| `bundle://proof/SB04/transcripts/anti-stub-audit.txt` | `new` | `AA5D8E61E318E66F2B0F4BF52DF3355963B67DAA2FD8B9761F46CEE80D5BC653` | Final anti-stub audit. |

## Command Transcripts

- Browser proof: `bundle://proof/SB04/transcripts/playwright-manager-chat-voice.txt`.
- Screenshot: `bundle://proof/SB04/browser/processes-manager-chat-voice-desktop.png`.
- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-manager-chat-voice.txt`.
- Passing final tests: `bundle://proof/SB04/transcripts/final-test-run.txt`.
- Anti-stub audit: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`.

## Semantic Adequacy Evidence

- Raw note owned: N006 "real demos/tests with voice mode"; final closure for N001 through N005.
- Browser proof: Playwright opened the fresh workspace-built local proof host on the Processes route, continued the startup database profile, opened Manager chat, asserted all three voice controls were enabled, clicked audio mode, and asserted `Audio on`.
- Screenshot review: controls are visible, aligned in the composer toolbar, enabled, and not clipped; audio status is visible below the composer.
- Scope note: real microphone recording and external provider calls were not exercised to avoid live credential and microphone-permission side effects; component tests cover record/speak callbacks and unit tests cover provider runtime dispatch.
- Anti-stub audit: `bundle://proof/SB04/transcripts/anti-stub-audit.txt` contains only test helper unsupported members, existing placeholders, and existing completed-task handlers; no new fake voice runtime was introduced.
