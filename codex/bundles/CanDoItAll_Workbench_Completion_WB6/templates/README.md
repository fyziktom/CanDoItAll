# Private working evidence

Copy `evidence.json` outside this sealed bundle and set `is_template` to false only while filling
real observations. Preserve original attempts; subsequent tests append independent records.
Never put secrets, keys, raw bearer URLs, customer content or full sensitive transcripts here.
An empty template deliberately fails `--require-demo-ready`. The helper checks consistency,
not authenticity or completeness of an application's security audit.
