# External evidence copy

Copy evidence.json outside the sealed bundle into the task-owned private artifact directory.
Record attempts as they occur, retaining failed originals and accepted native identities.
Do not put credentials, private contents or raw secret-bearing URLs in tracked documentation.
The closure checker establishes structural consistency, not truthful execution or authenticity.

A test attempt records id, kind=test, status, command, source_fingerprint (SHA-256), artifact_refs,
expected_discovery, actual_discovery, executed, passed, failed and skipped. Explain real theory
expansion. A non-test verification records its command, source fingerprint and private artifacts.
A qualified group includes notes, limitation and qualification_review with referenced attempts;
only groups expressly listed in bundle.json may use qualified closure.
