# Microsoft Graph Query Notes

Use official Microsoft Graph docs as source of truth.

Important points:

- `GET /me/messages` supports `$top`, `$select`, and OData query parameters.
- Use `$select` to keep message payload small.
- Use `Prefer: outlook.body-content-type="text"` when text body is required.
- `message.categories` is a string collection.
- Message `categories` can be updated via `PATCH /me/messages/{id}`.
- Outlook master categories should exist before assigning display names to messages.
- Be careful combining `$filter` and `$orderby`; Microsoft documents ordering constraints and `InefficientFilter` errors.

Implementation should handle Graph filter incompatibilities defensively with a bounded fallback.
