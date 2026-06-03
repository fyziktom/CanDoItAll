# Implementation Prompt

Implement `SB01 provider-model-pricing-settings` only.

Preserve strongly typed provider pricing models. Add a provider pricing refresh path that can load exact prices from API responses that include explicit pricing metadata, creates editable rows from model-name-only discovery, and preserves manual user rows. Keep UI code thin and route refresh through `WorkspaceService`. Capture targeted tests and proof under `proof/SB01/`, then update `reviews/01-execution-report.md`.
