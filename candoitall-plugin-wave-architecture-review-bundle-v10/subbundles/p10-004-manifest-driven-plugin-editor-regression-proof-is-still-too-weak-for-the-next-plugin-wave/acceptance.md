# Acceptance

This subbundle closes only when:
- the exact required unknown-plugin test names exist,
- the tests exercise `Text`, `Url`, `Number`, `Boolean`, `Json`, and `SecretReference`,
- no new page-specific key switch or editor-model property bag is introduced for the test plugins.

Target acceptance:
A new plugin manifest can be added and validated through the shared editor path without reopening shared pages.
