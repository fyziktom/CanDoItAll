# C# architecture review checklist

- [ ] Every new dependency points inward toward contracts.
- [ ] No MAF project references a product module.
- [ ] Runtime abstractions remain SDK-free.
- [ ] LLM abstractions remain agent/workspace/process/MAF free.
- [ ] No scoped service relies on an instance-local lock for a shared file resource.
- [ ] No service locator was introduced.
- [ ] No fallback implementation hides a required production registration.
- [ ] Authority parsing distinguishes absent from malformed.
- [ ] Profile identity and generation are validated where persisted authority is restored.
- [ ] Application transcript remains the ordinary-chat source of truth.
- [ ] Failed turns restore all turn-owned state.
- [ ] Usage is not discarded across retries.
- [ ] Public exceptions remain sanitized.
- [ ] Disposal/lifetime ownership is explicit and tested.
- [ ] Source files do not grow into a new God object; extract only cohesive collaborators.
