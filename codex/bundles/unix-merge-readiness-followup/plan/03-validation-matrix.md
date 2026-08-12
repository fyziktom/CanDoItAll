# Validation matrix

| Gate | Build | Focused tests | Runtime gate | Full stable | Host |
|---|---|---|---|---|---|
| M00 | none or metadata only | repository/source-contract | no | no | Windows |
| M01 | Builder + Persistence + affected host | plan hash/migration/tamper/restart | partial Unit/Integration | no | Windows |
| M02 | package + explicit source affected graph | dependency/capability contract | FileTools subset | no | Windows |
| M03 | ProcessHost + support host | orphan tree, PID reuse, timeout, dispose | process subset | no | Windows + Linux |
| C1 | clean solution package Release | M01–M03 affected set | full 422/33/1-equivalent catalog | Windows once, conditional | Windows; Linux focused |
| M04 | MCP projects | fake server ping, bounds, exit, timeout | MCP subset | no | Windows + Linux |
| M05 | Docker plugin + Web/Compose | strict parser, secret file, Compose contract | Docker subset | no | Linux/Docker |
| M06 | Core path/executable projects | symlink, X_OK, PATHEXT, controls | path/executable subset | no | Windows + Linux |
| C2 | clean package Release | M04–M06 set | complete on both | no | Windows + Linux |
| M07 | validation tooling | self-tests and stale-build negative proof | no product rerun unless tooling changes execution | no | Windows |
| M08 | PASS: clean solution + publish | PASS: all merge-readiness regressions | PASS: 468/468 both | one raw run each; P2 residuals disclosed | Windows + Linux |
| M09 | not run | handoff ready | not run | `MACOS NO-GO — environment` | actual macOS arm64 required |
| M10 | bookkeeping only | validators/checksums | M08 reused; no M09 host proof | no | bounded NO-GO review |
