# Historical negative proof

Repository state: detached worktree at pre-SB03 commit `61abf5bc3` with only the new regression test
added. Sibling project roots were pinned and repository-template output copying was disabled.

Command: focused `LlmChatWholeUseCaseProfileScopeTests` Unit test.

Result: exit 1; 0 passed, 1 failed, 0 skipped.

Failure:

```text
Profile_switch_after_first_read_rejects_authoritative_return [FAIL]
Assert.True() Failure
Expected: True
Actual:   False
...LlmChatWholeUseCaseProfileScopeTests.cs:line 73
```

Line 73 asserts that the runtime lease was acquired. The old public interface resolved the unscoped
application service, performed its repository read, observed the injected profile switch, and returned
without ever acquiring a lease. The same exact test passes at the SB03 implementation.

The disposable worktree was removed after capture. Git removed its metadata; Windows MAX_PATH required
verified long-path removal of the remaining worktree directory.
