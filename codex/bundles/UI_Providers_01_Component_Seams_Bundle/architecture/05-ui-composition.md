# UI composition and component contract

Large desktop target: 1600 x 1000. Keep existing ListDetailShell with 25rem catalog pane, compact icon toolbar/search, six secondary tabs and sticky per-form action footer. The detail region is the primary editing surface; catalog and detail retain their existing scroll owners. No summary-card expansion or visual redesign.

Use existing Tabs/TabsItem, Stack, Alert, EmptyState and Button. Typed definitions generate tab order; icons/labels match baseline. Core error replaces form with Alert and Retry in the detail region. Secret metadata warning appears above editable content and retains the unavailable saved option. Shared connections remains its existing lazy overlay.

Components MCP recommendation and component_get both returned Transport closed. Exact local read-only sibling contracts were inspected for Alert, EmptyState, Stack and Tabs. EmptyState has no attribute capture, so its test hook is on an existing Stack wrapper. No library edit, new import, package or asset registration.

Final proof must inspect normal and open-overlay screenshots, readability, spacing, sizing, clipping, first viewport and actual scroll behavior. No mobile scope.
