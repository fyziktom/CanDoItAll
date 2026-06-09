# Live Scenario Matrix

| Scenario | Required proof | Live provider allowed? |
| --- | --- | --- |
| Startup/template catalog | Integration test + API readback | No |
| UI launch | Playwright large desktop + API readback | No |
| Dispatch/finalizer/artifact | Deterministic fake provider first | No |
| MAF direct-agent smoke | Fake provider first, live OpenAI optional in SB019-SB021 | Yes, opt-in only |
| `.NET` create/modify | Deterministic fake/scaffold proof; live provider optional only for prompt path | Optional, bounded |
| Business analysis | Deterministic fake provider; live optional later only if explicitly approved | No in this bundle unless part of SB019-SB021 |
