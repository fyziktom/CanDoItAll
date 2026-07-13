# SB08 Proof Manifest

## Status

- Completed with integration fixture caveat.

## Build Proof

- `dotnet build src/Foundation/CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj --no-restore` passed with 0 warnings and 0 errors.
- `dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj --no-restore -p:BuildProjectReferences=false` passed with 20 NU1900 advisory-source warnings and 0 errors.

## Test Proof

- Focused unit command passed: 63 passed, 0 failed, 0 skipped.
- Focused integration command completed: 17 passed, 3 failed, 0 skipped. All three failures were `The selected agent does not have a provider profile.` before refactored MAF runtime code was exercised.

## Browser Proof

- Live app: `http://localhost:5032`.
- Viewport: 1600x1000.
- Routes:
  - `/agents` matched `agents-shell-tabs`.
  - `/agents?tab=agents` matched `agents-catalog-results`.
  - `/agents?tab=capabilities` matched `agents-capabilities-panel`.
  - `/agents/workflows` matched `workflows-tabs`.
  - `/processes` matched `processes-shell`.
- Browser errors: 0 page errors, 0 non-ignored failed requests.
- Screenshots:
  - `proof/SB08/screenshots/agents-shell-large.png`
  - `proof/SB08/screenshots/agents-chat-large.png`
  - `proof/SB08/screenshots/capability-setup-large.png`
  - `proof/SB08/screenshots/workflows-large.png`
  - `proof/SB08/screenshots/process-shell-large.png`
- Browser summary: `proof/SB08/browser-validation-summary.json`.

## Anti-Stub Audit

- Browser proof was run against the real local web app process, not mocked markup.
- No UI files changed, so narrow viewport rerun was not applicable.
