# QA Prompt

Validate the completed subbundle against the raw notes and the reference workbook. Treat a clean compile as insufficient unless the relevant route/menu/test/module-reference behavior is also checked.

Required checks:

- `rg` for direct old module references and old routes.
- Build/test command transcript with exit codes.
- Browser proof on `http://localhost:5032/` after the app is rebuilt and restarted.
- Screenshot or DOM evidence that shell navigation no longer exposes the removed module routes.
- Explicit note for any remaining historical migration references kept by design.
