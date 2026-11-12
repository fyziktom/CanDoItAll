# File publication and explicit recovery

Windows authority-file readers retain a complete snapshot through an open read
handle with read/delete sharing. Existing-target publication uses `File.Replace`;
new readers observe the replacement while a held reader keeps its original bytes.
CreateNew still refuses existing files. Unix replacement retains its existing
rename path. These are visibility guarantees, not power-loss durability claims.

Immediately before Windows replacement, the writer renames its flushed staging
file from `.target.<guid>.tmp` to `.target.<guid>.pending`. A successful replacement
consumes it. Documented nontransition failures may retry within the bounded write
window, then remove only that attempt's staging file. Partial Windows replacement
errors 1176/1177 and unclassified replacement failures preserve the pending file and
raise `DurableFilePublicationRecoveryException`. A crash during publication can
leave the same evidence. See the
[Windows replacement contract](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew).

Both later writes and deletes check target-specific pending evidence under the
existing exclusive coordination before stale temporary cleanup. They fail explicitly
until recovery. Unrelated targets remain writable. A complete old target or staged
new payload alone does not establish whether the owner accepted the mutation.
Postpublication cleanup or observer failure also does not establish rollback;
the owner must reconcile its existing revision or receipt.

For recovery, stop affected writers and preserve the target, pending and backup
files. Inspect complete contents and the owner's authoritative revision or receipt.
Restore the authoritative version under exclusive coordination, then remove only
the inspected pending file. Do not automatically republish pending bytes or delete
them as stale files. These files may contain the target's sensitive data and require
the same access protection. The exception exposes the exact recovery path without
embedding it or the payload in its message.

This adds one same-directory rename per Windows replacement and no extra flush,
database call or reader lock. The existing write-directory scan checks pending
evidence before cleanup; explicit deletion also performs this check. Sustained
authority capacity measurements remain a separate acceptance gate.
