# Normalized Requirements

## Shared Component Requirements

- `R001` The BaseLib `Tabs` component must stop depending on the shared `zy-*` styling contract. The shipped shared contract may use only the existing CanDoItAll `cad-*` and `cda-*` Tailwind/token families.
- `R002` The shared tabs look must be Tailwind-owned. Do not hand-edit `wwwroot/css/output.css`; update Tailwind source files and rebuild the generated CSS.
- `R003` The tabs component must preserve working keyboard navigation, focus movement, disabled handling, icon and badge rendering, tab positions, and server/client render modes while the styling contract is refactored.
- `R004` Tabs appearance customization must be parameter-driven, not page-hack-driven. The component must expose a root `Class` extension point and at least one enum-backed appearance path for additional look tuning.
- `R005` The preferred light tab-button border treatment must exist as an optional parameterized appearance choice rather than a permanent hard-coded default.
- `R006` Missing tab text must degrade intentionally and remain readable instead of producing broken layout or empty chrome.

## Sandbox And Discovery Requirements

- `R007` The sandbox must gain a dedicated tabs route or page focused on the shared tabs component rather than only a mixed navigation page section.
- `R008` The tabs sandbox surface must include both healthy and non-optimal examples, including:
- long titles
- missing title fallback
- wrapping or overflow stress on a narrow column or narrow viewport
- the optional border treatment
- at least one state comparison that helps reveal spacing, active-state, or disabled-state defects
- `R009` The example surface must be used as a defect-discovery tool. If the new examples expose foundational issues, the earlier shared-component subbundle must be reopened and repaired before closure.

## Reference And Styling Constraints

- `R010` Radzen may be used as a behavioral and visual reference only. The final BaseLib implementation must not ship Radzen classes, Sass, or JS.
- `R011` The final tabs appearance must feel coherent with the repo’s current shared visual system and `cad` token family, not like the existing purple `zy` stylesheet transplanted into a new file.

## Proof Requirements

- `R012` The work must ship with targeted regression proof, including focused component tests for the shared tabs contract.
- `R013` The final UI proof must use real browser automation plus screenshots on a large-screen pass and narrower-width follow-up passes.
- `R014` The execution report must explicitly record browser-validation analytics, subbundle gate results, and raw-note closure status before the bundle can close.
