# Validation and browser proof

## Status

- `Completed`

Closure proof: `proof/SB04/manifest.md` and `proof/SB04/semantic-invariants.md`.

## Objective

Validate the full change set with tests, clean build, restarted 5032 instance, and Playwright proof through the right-click Generate image path.

## Success Criteria

- Targeted tests pass.
- Clean build passes.
- Port 5032 runs the rebuilt application.
- Browser proof uses right-click node context Assets -> Generate image.
- The provider list contains image providers.
- The generated-image form accepts prompt text and creates a waiting node immediately.
- Completion succeeds with local ComfyUI, or ComfyUI connection failure is reported as the explicit blocker requested by the user.

## Covered Inputs

- Requirement R10 and final raw-note closure.

## Prerequisites

- SB01, SB02, and SB03 closure gates passed.
- No known build break.

## Exact Source References

- `C:/repositories/CanDoItAll/src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`
- `C:/repositories/CanDoItAll/.codex/bundles/project-structure-deferred-image-creation/reviews/01-execution-report.md`

## Deliverables

- Build/test transcripts.
- 5032 restart transcript.
- Playwright screenshot(s) and DOM/action assertions.
- Final execution report and raw note closure.

## Dependency Impact

- This is the final closure gate. Any failed proof reopens the owning subbundle.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run targeted generated-image/project-structure tests.
2. Run clean web build.
3. Stop any stale 5032 process and restart the rebuilt app on 5032.
4. Use Playwright MCP/browser automation on the right-click flow.
5. Capture screenshots and assertions.
6. Update execution report and final bundle status.

## Scope Exceptions

- If ComfyUI is unavailable or the API is not reachable, stop and report the blocker. Do not continue with mock proof as if local generation works.

## Do Not Do

- Do not validate only by direct service invocation.
- Do not skip browser open-state proof for the floating create window/dropdown.
- Do not leave the 5032 process in an unknown state.

## Acceptance Checklist

- [ ] Tests pass.
- [ ] Build passes.
- [ ] 5032 restarted from current build.
- [ ] Browser path uses right-click context menu.
- [ ] Waiting node visible immediately.
- [ ] Completion or explicit Comfy blocker recorded.

## Proof Required

- Command transcripts in `proof/SB04/`.
- Playwright screenshots in `proof/SB04/`.
- Updated `reviews/01-execution-report.md`.
- Completed raw note closure table.

## Browser Validation Logging

- Route: `http://localhost:5032/projects/{projectId}/structure`.
- Viewport: maximized or desktop at least 1440px wide.
- Required actions: right-click target node, open Generate image from context action, enter prompt, choose provider, submit, observe waiting node, observe completed image or failure.
- Screenshot review questions: provider dropdown visible and populated; create dialog not clipped; waiting placeholder readable; final node remains in same graph position.

## Progression Gate

- Final closure can proceed only after test/build/browser proof is recorded or a ComfyUI blocker is documented exactly.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
