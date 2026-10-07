# External evidence

Copy `evidence.json` outside the sealed package into an owned private evidence directory.
Fill real attempts and group dispositions there; never prefill a successful product run.
A group may reference several attempts, including retained failed and successful follow-ups.
Use distinct command/source/image identities for each checkpoint. Keep historical failures.

A passing test attempt needs command, SHA-256 source fingerprint, artifact references,
expected and actual discovery, executed/passed/failed/skipped counts. Any discovery versus
execution expansion must be explained. Manual browser/audit observations must name their
actual procedure and proof, not masquerade as discovered automated tests.

The parser does not inspect remote artifacts or authenticate execution. Its success is
only internal consistency. Qualified or blocked groups cannot produce an unqualified
complete result. The original package template must remain NOT_RUN.
