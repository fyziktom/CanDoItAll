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
| M08 | clean solution + publish | all merge-readiness regressions | complete on both | once each | Windows + Linux |
| M09 | package Release/publish | macOS focused catalog | complete macOS | colleague decision | macOS arm64 |
| M10 | none if bookkeeping only | validators/checksums | reuse M08/M09 | no | review |
