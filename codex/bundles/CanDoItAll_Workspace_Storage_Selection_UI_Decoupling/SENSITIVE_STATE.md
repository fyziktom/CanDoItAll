# Sensitive state and authority constraints

This feature edits references, not credentials or effective grants. Its safe data boundary is narrower than Storage catalog administration and much narrower than the Agent runtime.

Do not query or carry secret payloads, private connection configuration, vault references, bearer tokens, password hashes, file grants or raw driver responses. A credential reference is not required just to label a catalog. Do not copy raw exception messages into a picker, notification, test name or URL. Use safe availability diagnostics and keep internal failure types only in appropriate redacted diagnostics.

The existing UI intentionally shows catalog labels and endpoint/root descriptions. Preserve useful operator information with a safe projection, but remove embedded credentials from URI-like values and never derive identity from a display string. Test hostile/sentinel exception/configuration material without retaining it in screenshots or logs. Readable metadata is not a newly granted permission.

The parent owns four separate facts: CanReadStorage, CanWriteStorage, AllowAllStorageCatalogs and the explicit ID list. Picker Apply only updates the last fact through the current parent. Parent normalization and runtime authorization remain canonical. No selected/empty/missing/disabled state may implicitly turn on any flag.

The runtime already checks allowed catalogs and disclosure against actual owner facts. A successful metadata read cannot waive those checks. Test filtered/denied results using safe private fixtures and unchanged runtime policies; do not add an allow-all production registration to make a browser journey convenient. [WS08, WS12]

Retire child dialogs and metadata when the parent/source lifetime changes. A failed old read must not restore metadata or invoke a successor callback. A cancellation signal is a request to stop, not proof that a queued continuation cannot run. Keep exact ownership checks after awaits and idempotent cleanup. [WS01, WS03, EV09]

Cancel/parent close cannot revoke a saved Agent permission or undo an already accepted parent save. Conversely, closing a picker must not save its staged selection. The extraction adds no durable operation, replay queue, privilege token or user-session registry.

Use owned profiles/control-plane roots, ephemeral loopback hosts and harmless files. Never read or mutate the ordinary application, real account store, vault, retained customer files or other processes. Exclude revealed secret fields from retained screenshots. Artifact scans must state text-size/readability/excluded-type limits rather than implying an unbounded scan.
