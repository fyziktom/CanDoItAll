# Closure Checklist

Before closing any critical subbundle:

- [ ] Proof manifest exists under `proof/SBxx/`.
- [ ] Raw note is quoted literally.
- [ ] Changed production file hashes are recorded.
- [ ] Changed test file hashes are recorded.
- [ ] Failing-first transcript exists and has non-zero exit code.
- [ ] Passing transcript exists and has zero exit code.
- [ ] Source-level assertions are listed and verified.
- [ ] Anti-stub scan transcript exists.
- [ ] Downstream dependency impact is checked.
- [ ] Red-team verifier either passed or the subbundle is not final-closed.
