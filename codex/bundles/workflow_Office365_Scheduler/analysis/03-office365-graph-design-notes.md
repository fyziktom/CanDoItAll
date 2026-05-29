# Office365 Graph Design Notes

## Read query

Target executor: `office365.messages-by-address-unprocessed`

Minimum settings:

```json
{
  "connectionId": "",
  "emailAddress": "person@example.com",
  "processedCategory": "CanDoItAllProcessed",
  "mailFolderId": "",
  "matchMode": "FromOrSenderEquals",
  "maxMessages": 1,
  "lookbackHours": 336,
  "includeBody": true,
  "maxBodyCharacters": 60000,
  "noMatchBehavior": "SuccessNoMessages"
}
```

Preferred Graph behavior:

- Use `GET /me/messages` or `/me/mailFolders/{id}/messages`.
- Use `$top=1`.
- Use `$select=id,subject,from,sender,receivedDateTime,bodyPreview,body,categories,webLink,conversationId,internetMessageId`.
- Use `Prefer: outlook.body-content-type="text"`.
- Use a server-side filter when Graph accepts it:
  - match sender/from address;
  - exclude messages where `categories/any(c:c eq '{processedCategory}')`;
  - optionally constrain `receivedDateTime ge {utc}`.

Fallback behavior:

- If Graph returns an unsupported/complex filter error, query a bounded candidate page by lookback and category exclusion, then apply the email-address match client-side.
- Never fetch unbounded mailboxes.
- Never fetch more than the configured candidate cap.

## Category mutation

Target behavior:

- Reuse or extend `Office365MarkProcessedWorkflowExecutor`.
- Allow `sourceCategory` to be empty.
- Ensure master category exists.
- PATCH message `categories` with the previous categories plus processed category.
- Preserve existing categories unless explicitly configured to remove a source category.

## Payload shape

The download executor must produce:

```json
{
  "provider": "office365",
  "filterKind": "emailAddress",
  "filterValue": "person@example.com",
  "processedCategory": "CanDoItAllProcessed",
  "count": 1,
  "messages": [
    {
      "id": "...",
      "internetMessageId": "...",
      "conversationId": "...",
      "subject": "...",
      "from": "person@example.com",
      "sender": "person@example.com",
      "receivedAt": "...",
      "bodyText": "...",
      "bodyPreview": "...",
      "categories": [],
      "webLink": "..."
    }
  ],
  "office365Processing": {
    "connectionId": "...",
    "processedCategory": "CanDoItAllProcessed",
    "messageIds": ["..."],
    "selectedMessageId": "...",
    "idempotencyKey": "office365:<message-id>"
  },
  "projectId": "...",
  "nodeId": "...",
  "runContext": { "...": "..." },
  "workflowInput": { "...": "..." }
}
```

When there is no matching email, return:

```json
{
  "provider": "office365",
  "filterKind": "emailAddress",
  "filterValue": "person@example.com",
  "processedCategory": "CanDoItAllProcessed",
  "count": 0,
  "messages": [],
  "noMessages": true,
  "route": "no_messages",
  "summary": "No unprocessed Office365 email matched the configured address."
}
```
