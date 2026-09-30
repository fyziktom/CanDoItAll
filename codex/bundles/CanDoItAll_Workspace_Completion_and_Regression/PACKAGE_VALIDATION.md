# Handoff validation

This document reports package checks only, not CanDoItAll execution. The source review did not run .NET, PostgreSQL, a browser, a live model, or a product benchmark.

The package validator checks UTF-8 text/JSON, safe paths, no symlinks, local Markdown file links, declared documents, source IDs/provenance structure and the complete SHA-256 manifest. It does not verify Markdown heading anchors or fetch external URLs.

The campaign validator is tested with temporary synthetic fixtures for missing/duplicate cases, incomplete templates, exact checkpoint requirements, evidence hash changes, unsafe paths, wrong modes, false live passes, absent owner/UI proof, failed retry accounting and budget violations. These tests are not live provider executions.

Observed source-tree checks: 50 files, seven JSON documents, 43 local Markdown file links, 33 source records and 49 top-level manifest entries. All 22 shared v3 files match the prior handoff byte-for-byte. The 11 package-tool tests, 14 shared-tool tests and 19 campaign-validator tests passed with zero failures or skips (44 tooling tests, not product tests). ZIP CRC and fresh extracted-copy validation are the final delivery checks. The initial 29-group campaign template is intentionally NOT_RUN; structural validation accepts it but complete validation rejects it.

```text
python tools/validate_package.py
python tools/test_package.py
python shared/tools/test_tooling.py
python tools/test_campaign.py
python tools/validate_campaign.py --plan campaign-plan.json --results templates/campaign-results.json
```

Do not convert these package passes into a claim that the user application works. Copy the blank campaign results into the ignored working evidence directory and populate actual product attempts there, leaving this sealed handoff unchanged.
