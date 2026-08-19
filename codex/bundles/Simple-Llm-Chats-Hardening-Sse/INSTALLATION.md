# Installation and use

Place the extracted folder at:

```text
codex/bundles/Simple-Llm-Chats-Hardening-Sse
```

Validate preparation from the bundle root:

```powershell
python ./scripts/validate_bundle.py --bundle-root .
python ./scripts/check_traceability.py --bundle-root .
python ./scripts/check_test_policy.py --bundle-root .
```

After source work begins:

```powershell
python ./scripts/check_architecture_boundaries.py --repo-root ../..
python ./scripts/check_sse_contract.py --repo-root ../..
```

Adjust `--repo-root` to the repository root. Do not execute all subbundles as one undifferentiated task.
