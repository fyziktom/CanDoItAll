# Dependency direction
Existing edges only: UI -> provider application/runtime ports and Models; ProviderManagement -> Core/Models/Infrastructure/Security/shared-provider protocol; Web -> product application composition. Models cannot import ProviderManagement or UI. No new project or partial file.

Optional editor expected-token metadata belongs in Models; provider persistence/commit failure contracts belong at the existing ProviderManagement boundary. The registry interface may gain a narrowly justified projection reconciliation operation only if it can be implemented by all real registries without fake fallback. Prefer a cohesive production adapter over interface-per-method proliferation.

No change to sibling source mode, Tailwind pipeline or asset graph. Final direct builds cover every edited owning csproj, with broader checkpoint triggered by the actual shared editor serialization/concurrency and shared persistence/outcome contracts, not by task size.
