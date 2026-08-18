# Using this bundle

The ZIP contains one bundle directory. Place or rename it to:

```text
codex/bundles/Simple-Llm-Chats-Backend-Api
```

inside a current checkout of `fyziktom/CanDoItAll`.

Then run:

```powershell
python ./codex/bundles/Simple-Llm-Chats-Backend-Api/scripts/validate_bundle.py --bundle-root ./codex/bundles/Simple-Llm-Chats-Backend-Api
python ./codex/bundles/Simple-Llm-Chats-Backend-Api/scripts/check_test_policy.py --bundle-root ./codex/bundles/Simple-Llm-Chats-Backend-Api
```

Start with `CODEX-EXECUTION-CONTRACT.md` and SB00. The prepared baseline commit is not a request to
reset the repository; the executor must re-anchor to the current branch.
