# Large-Screen Only Proof Policy

The user explicitly asked not to spend time testing small or medium screen optimizations. This work targets PC/large-screen operation only.

Rules for this bundle:

- Do not create small-screen, medium-screen, mobile, phone, tablet, Android, or iPhone screenshot/proof artifacts.
- Do not run responsive optimization tests unless the implementation unexpectedly changes UI layout and the user gives a new requirement.
- If UI proof is needed, use a large desktop viewport only, preferably maximized headed browser or equivalent large-screen Playwright viewport.
- Most subbundles are runtime/service/code refactors and should record browser validation as N/A.
- Add final scans that fail on proof paths containing `mobile`, `small-screen`, `medium-screen`, `phone`, `tablet`, `android`, or `iphone`.
