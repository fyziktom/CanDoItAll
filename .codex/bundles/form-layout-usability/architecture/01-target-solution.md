# Target Solution

## Shared Foundation

- Update BaseLib form wrappers to make stretching and textarea defaults the norm:
  - `FormField` child content wrapper should be `min-w-0 w-full flex-1`.
  - `TextArea` should default to a larger row count.
  - `.cda-input--textarea` should set shared `display`, `width`, `resize`, and `min-height` defaults while allowing explicit `min-h-*` or page CSS to override upward.
- Add lightweight enterprise scan cues to `FormSection` through an optional icon parameter and restrained section header treatment. Existing callers do not need to change to benefit from default form polish.

## Targeted Module Patterns

- For process definition and step editors, split dense long-text sections into tabs such as identity, governance, contracts, and simulation where that reduces scanning cost.
- For CRM-HR forms, make long-text fields span available width or pair only when they are genuinely comparable fields.
- For workspace settings and project-structure secret dialogs, keep metadata and payload as distinct regions while allowing value/textarea rows to use available width.
- For prompt factory and agent framework editor surfaces, avoid shrinking long text editors in floating or modal contexts.

## Browser Proof Strategy

- Use a large desktop viewport first, then a narrower viewport for forms whose layout changes responsively.
- Capture form-only screenshots by clipping to form containers or by cropping saved screenshots after capture.
- Compare actual screenshots against proposal goals: stretch, readable textareas, topical grouping, visible actions, no clipping, no incoherent overlaps.
