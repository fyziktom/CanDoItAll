# Execution Skill Hardening Architecture

## Why previous execution passed too early

The previous bundle contained strong language, but its gates were not executable enough. The agent could:

- add a class or enum matching the requested concept;
- add a happy-path test that asserts the shallow output;
- update an execution report to `Completed`;
- pass structural bundle validation;
- run a build and a narrow unit test suite;
- still miss the actual semantic requirement.

## Required new execution contract

Every deep implementation subbundle must provide:

1. **Literal raw-note closure:** quote or paraphrase the raw user requirement and state exactly how the shipped behavior satisfies it.
2. **Shallow-pass trap:** identify the easiest shallow implementation that would pass naive tests, then add a test that fails that shallow implementation.
3. **Adversarial negative test:** prove that the system rejects, splits, defers, or flags harmful cases.
4. **Semantic positive test:** prove the intended behavior on a realistic case.
5. **Dependency recheck:** if a later subbundle relies on an earlier foundation, re-run one dependent-flow proof.
6. **Anti-stub audit:** list any placeholder, template-only, hard-coded, or TODO-like implementation left in the path.
7. **Evidence mapping:** cite source files and behavior proof in the execution report.

## Required new validator behavior

Completed-stage validation must fail when:

- execution report rows do not include semantic proof identifiers;
- raw note closure says solved without proof paths;
- critical subbundles do not list an adversarial negative test;
- a required skill-installation subbundle has not reopened and cited updated skills before downstream work;
- browser proof is used as a substitute for backend semantic proof;
- tests assert internal template markers rather than user/domain behavior.
