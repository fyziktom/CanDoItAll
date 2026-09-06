# Isolated catalog measurement fixture

This setup-only console project uses the repository test bootstrap to create a new isolated PostgreSQL profile, seed the production catalog and add representative local rendering fixtures. It is outside the product and sandbox dependency graphs.

Run it once with the absolute repository root argument, using the repository testing prerequisites. It writes the generated environment, private credential file and original catalog snapshot under ignored task data. It must never use an operator database, and its credential file must never enter proof.

Keep one unchanged fixture for both full-app timing phases. Export and verify the sanitized rendering snapshot before freezing either host; do not rerun setup between samples. See the [measurement reproduction instructions](../README.md) for the exporter, launch profiles, asset pipeline and cleanup ownership.
