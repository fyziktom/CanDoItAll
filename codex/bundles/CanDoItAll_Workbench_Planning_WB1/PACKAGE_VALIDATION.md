# Package validation

This is an implementation handoff, not product test evidence. All required product groups
start as NOT_RUN with no attempts. The optional completeness check must reject that template.
The 22 shared-v3 files are retained byte-for-byte and the immediate source archive hash is
recorded separately. Source records distinguish whole-file reads, partial ranges and inventories.

Local checks cover exact SHA-256 manifest coverage, local Markdown links, shared provenance,
source identities, evidence shape and the source-reference markers used by handoff documents.
Unit tests exercise positive structure fixtures and rejection of incomplete/contradictory
reports. An internally consistent synthetic test fixture is not actual application evidence.

Final packaging also validates a newly extracted copy and ZIP CRCs. See package-validation.json
for the observed package metrics. No .NET, PostgreSQL, browser, Docker or watch run is claimed
by those checks. Product originals and credentials are not included.
