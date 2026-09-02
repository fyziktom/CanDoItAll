# Original Application Branch Delta Inventory

| Commit | Intent | Integration decision |
|---|---|---|
| `168d367c2fcd65e0aafc304462c4f88a26a07807` | Ignore Rider `.idea/` | Keep |
| `46b8cb63ade904eec7f4d96bfba537b4473ba75a` | SDK downgrade to `10.0.204` | Reject; keep current development SDK |
| `923298130cbb9f1c1c917f12ca548153a17f37bc` | Root `npm run watch` | Keep |
| `bfa655113ca114afef900ec64ce1df905d7a38ab` | Material Symbols CSS asset | Keep and complete downstream migration |
| `a2903c400cc35e6d1d2f233c51e73feb256ce2aa` | Podman/macOS guide | Modernize and relocate |

No other original-branch code is expected. If execution discovers additional unique commits,
stop and refresh scope analysis.
