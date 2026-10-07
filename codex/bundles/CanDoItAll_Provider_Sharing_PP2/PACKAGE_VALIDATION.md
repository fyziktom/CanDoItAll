# Package validation and evidence limits

This package is generated from connector-read repository files at the recorded commit and the
uploaded PP1 package. No application C# build, PostgreSQL test, browser, Docker runtime or watch
benchmark was executed by the package author. The environment lacks the relevant runtime tools.
The private implementation TRX/logs/media were not supplied. Repository reports are attributed
implementation evidence, not independently replayed results.

`tools/validate_package.py` checks local UTF-8/JSON, declared inputs, safe links, source references,
manifest, exact shared v3 bytes and untouched NOT_RUN evidence. `tools/check_model_parity.py`
checks only identity-keyed consistency of supplied safe snapshots; it cannot authenticate that
containers/browser requests really ran. Its negative controls do not prove the product's mapping.

Before distribution, run package unit tests and the unchanged shared tooling tests, validate a
freshly extracted archive and compare every shared file to the input PP1 archive. The public
response reports actual resulting counts; this note deliberately does not prefill them.

Local source URLs are pinned evidence references. Repository changes at execution time must be
reviewed; do not reset to the audited SHA. No private credentials, screenshots, TRX, signing keys
or binary dependencies are embedded. Metadata permits only local signed Git commits and owned
isolated test publication; it authorizes neither push nor external source publication/spending.
