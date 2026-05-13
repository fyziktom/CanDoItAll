# Review Gates

## Foundation Review Gate: SB08

Codex must stop after `SB01`-`SB07` and answer:

1. Is there one canonical settings schema/state/validator?
2. Did workflow executor descriptors gain enough plugin provenance/availability metadata?
3. Are current workflows still backward compatible?
4. Can a plugin executor be rejected when disabled/unavailable/incompatible?
5. Are secrets consumer-bound by plugin/executor/connection?
6. Are storage/workspace/project-structure services exposed only through facades?
7. Did helper code end up in canonical services rather than pages?
8. Are duplicate registration paths resolved or explicitly documented?
9. Is the plugin module still unimplemented?
10. Are tests and proof captured?

## MVP Review Gate: SB14

Codex must stop after `SB09`-`SB13` and answer:

1. Does the plugin module remain separate?
2. Are bundled plugins statically registered and deterministic?
3. Is plugin catalog state separate from plugin connection state?
4. Are workflow node settings separate from plugin settings?
5. Does workflow executor catalog display plugin executors with source/availability?
6. Does workflow editor use renderer registry/schema fallback, not hard-coded plugin branches?
7. Are sample plugin secrets resolved through broker only?
8. Are sample plugin outputs sanitized and size-bounded?
9. Are UI and API proof artifacts captured?
10. Are shop/dynamic loading still out of scope?

## Final Review Gate: SB18

Codex must stop after `SB15`-`SB17` and answer:

1. Is shop/package metadata implemented without arbitrary executable-code loading?
2. Does OAuth2 extension point avoid breaking future SaaS plugins?
3. Are there tests for duplicates, disabled plugins, invalid settings, secret authorization, and workflow invocation?
4. Are browser screenshots reviewed for plugin catalog/settings/workflow executor selection?
5. Is documentation updated with MVP scope and future restrictions?
6. Are all scope exceptions explicit?
7. Is the implementation ready for a follow-up OAuth2/SaaS plugin bundle?
