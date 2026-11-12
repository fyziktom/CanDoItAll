# Project cleanup notices without displacing the workspace

Completed project deletions can retain several media references per operation.
These are durable completion records: immutable storage retention is an expected
provider outcome, not a failed project deletion. Expanding every receipt into a
warning above the portfolio obscures the primary project surface. Completed history
therefore uses a compact summary and an explicit review dialog.

The Projects service and Workbench storage participants retain ownership of deletion,
retry, receipts and authorization. Their durable records and HTTP contracts remain
unchanged. The routed Projects host owns the review dialog lifetime, inventory reads,
operation feedback and exact retry target. The existing Projects UI renderer owns
compact presentation and bounded paging. No new project, interface, dependency,
partial class or background mutation is needed.

Use the existing BaseLib Alert, Cluster, Stack, TextBlock, Button, SectionHead and
Dialog contracts. A compact summary distinguishes unfinished cleanup from completed
operations with retained media. The project cards remain useful in the first desktop
viewport. Details open explicitly in a wide dialog; its body owns scrolling. Pending
operations retain exact retry controls and busy/refusal feedback. Completed history
is informational and paged, with media details available without expanding the page.
Closing this view never acknowledges, deletes or retries a record.

The dialog body captures its non-null review snapshot when it is created. A queued
child render may run after the host clears the review on close; reading the live
nullable parameter inside that fragment can otherwise terminate the Blazor circuit.
The close regression also renders that queued body after the host has cleared it.

Post-operation feedback is concise; exact retained objects and remediation remain in
the receipt view. Reading fresh inventory cannot turn successful immutable retention
into an error. Unknown outcomes still require observation before another write, and
failed cleanup must remain visible even when completed history is also present.

Regression proof covers many receipts and long messages, bounded initial markup,
explicit details and paging, close/reopen, refreshed inventories, exact retry identity,
busy/unknown outcomes, and coexistence of pending and completed operations. The real
Projects host plus a large-history sandbox scenario verify the component boundary.
Desktop browser evidence must show the portfolio in the initial viewport and the
dialog with usable close/retry controls, no lateral overflow, and no page displacement.
Affected production builds, focused discovery/execution and portability-static close
the change. No receipts or media are erased to obtain a clean screen.
