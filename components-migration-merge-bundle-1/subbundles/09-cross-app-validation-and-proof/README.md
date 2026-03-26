# 09 Cross-App Validation And Proof

## Objective

Run the final validation wave after both apps are wired to the new shared library layout. No phase is complete without proof.

## Required Build And Test Surfaces

### CanDoItAll

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright`

### Zyphonote

- `C:\repositories\Zyphonote\tests\App.Web.PlaywrightTests`
- `C:\repositories\Zyphonote\tests\App.PdmxTool.PlaywrightTests`

## Required Screenshot Surfaces

### Sandbox

- every group page
- at least one dense and one empty scenario per group
- desktop and mobile

### CanDoItAll

- main layout
- representative module pages using shared surfaces
- canvas pages

### Zyphonote

- account marketplace
- account playlists
- account events
- account learning builder
- account learning package
- account my scores
- account seller profile
- any page whose layout changed because of shared-component adoption

## Required Validation Questions

- can I read all texts properly?
- will I like and understand this UI/layout as a new user?
- is there any too large component, gap, or visual disruption?
- do we use proper components from shared libraries instead of custom ad-hoc markup?
- do we use available space properly?
- can the page be understood by scanning headings only?
- does the hierarchy remain clear without decorative effects?
- do desktop and mobile layouts both feel intentional?
- are focus, hover, disabled, loading, and empty states coherent?
- did any app accidentally keep a dependency on old shared paths or styles?
- did any shared component regress into app-specific styling debt?

If any answer is negative or uncertain, the agent must tune the implementation and re-run proof.

## Required Proof Artifacts

- build logs for both repos
- component/unit test results
- Playwright results
- screenshot set
- short regression notes for any compatibility shim still present
- final sign-off note covering QA, architecture, and delivery readiness

## Exit Criteria

- both repos build
- shared component tests pass
- Playwright smoke/regression coverage passes for critical flows
- screenshots are reviewed
- no open blocker remains around ownership, asset resolution, or visible UI regression

## Suggested Agent Prompt

```text
Implement subbundle 09 only.

Run the final proof wave for the shared component migration. Build and test both repos, collect screenshots for sandbox plus both apps, answer every validation question from the bundle, and only accept the migration if QA, architecture, and delivery readiness are all satisfied. If any concern appears, fix it and repeat proof before signing off.
```
