# Large-Screen-Only Proof Policy

This work is runtime/service refactoring and should not touch rendered UI. Browser validation is expected to be `N/A`.

If an implementation unexpectedly affects rendered UI or requires visual proof, Codex must:

- Use only a large desktop/PC viewport.
- Avoid mobile, tablet, small-screen, and medium-screen screenshots.
- Record why UI proof was needed.
- Record browser analytics only for the large-screen proof.

Do not spend cycles optimizing or screenshotting small and medium screen layouts in this bundle.
