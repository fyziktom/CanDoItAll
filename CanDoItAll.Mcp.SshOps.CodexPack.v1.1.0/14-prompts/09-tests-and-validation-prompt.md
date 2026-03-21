# Prompt: tests and validation

Add the test projects and validation harness described in the package.

Minimum:
- unit tests for security-sensitive behavior,
- contract tests for every public tool,
- integration tests with a fake transport,
- a documented and scriptable E2E smoke path.

For every real-host E2E run, start by auditing the target facts:
- distribution and version,
- CPU architecture,
- glibc / OpenSSL / ICU baseline,
- Docker/Compose availability,
- systemd availability,
- port availability,
- whether containers are allowed on that host at all.

If the host does not match the Ubuntu plus Docker baseline, switch to the documented fallback lane instead of forcing the wrong plan. The fallback lane must still validate:
- HTTPS exposure through Traefik,
- private IPFS behavior,
- application health,
- browser proof from a client machine with Playwright screenshots,
- a short failure analysis that explains why the lane changed.

Fix every failing test at the root cause, not with a workaround.

At the end, report:
- what is covered,
- what remains a known limitation,
- how to run the tests,
- whether any plan, prompt, or checklist updates were required by field validation.
