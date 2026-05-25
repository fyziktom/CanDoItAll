# Preparation review

This follow-up bundle is intentionally stricter than the first pass. The previous implementation removed most heavy SQLite infrastructure, but preserved SQLite as a legacy typed state. That does not match the user's updated direction: remove SQLite from the main CanDoItAll runtime completely.

The highest-risk fix is legacy catalog handling. Removing enum values without a raw JSON quarantine path may make older control-plane catalogs fail deserialization/startup. Therefore SB01 must be completed before UI cleanup and before final residue checks.

The second highest-risk area is PostgreSQL migration drift. The branch's consolidated baseline must be validated against the current model after all cleanup is complete.

The third highest-risk area is process/workflow runtime. PostgreSQL-only persistence gives freedom to use row-level locking and stronger claim patterns; this should be a dedicated subbundle rather than an incidental cleanup.
