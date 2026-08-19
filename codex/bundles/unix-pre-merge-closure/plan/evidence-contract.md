# Evidence contract

Retain only bounded, reproducible evidence:

- exact repository commit;
- target development commit;
- dirty/clean status;
- package/source dependency mode;
- SDK version;
- runtime catalog version;
- source fingerprint and test assembly hashes;
- TRX counters and exact selected classes;
- migration fixture identities and classifications;
- Docker image identity and `setsid` probe result;
- app/database health result;
- static scan summaries;
- redaction/secret-scan summary;
- final decision.

Do not retain:

- database passwords;
- environment variable values;
- raw provider/API credentials;
- full process stdout/stderr when a bounded result is enough;
- unnecessary absolute developer paths;
- Docker secret files;
- screenshots unless UI validation becomes necessary.
