# QA Prompt

Review the active subbundle against the raw request and normalized requirements.

Coverage checks:
- Verify every raw note N001 through N006 is either solved, partially solved with proof, or explicitly blocked.
- Confirm Manager chat voice buttons are enabled only when the selected manager agent allows voice mode.
- Confirm normal voice-denied behavior remains for voice-disabled agents.
- Confirm provider runtime STT and TTS dispatch through typed provider drivers.

Proof review:
- Do not accept prose-only closure for critical subbundles.
- Verify proof manifest paths exist, transcripts include command and exit code, and invariant IDs appear in transcript output.
- For browser proof, verify route, viewport, actions, assertions, screenshot paths, and visual review questions are recorded.

Blocker handling:
- If real microphone/audio playback cannot be validated, record the browser/host blocker and ensure component/unit proof still covers JS interop and service callbacks.
