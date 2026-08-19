# Branch and commit discipline

- Work on the requested branch or an implementation branch based exactly on the reconciled `simple-chats` head.
- Record the base SHA for every subbundle.
- Keep each subbundle in one or more coherent commits that do not mix downstream Simple Chat work.
- Do not amend closed checkpoint commits after downstream work begins without reopening proof.
- Keep generated bundle proof and status updates in the same logical closure commit as the subbundle outcome.
- Do not commit build outputs, test result caches, browser binaries, `.pyc`, or `__pycache__`.
