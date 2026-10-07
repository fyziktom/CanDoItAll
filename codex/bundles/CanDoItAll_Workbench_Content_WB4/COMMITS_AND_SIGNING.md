# Coherent signed checkpoints

At entry inspect the repository's actual signing setup and available native key. Ask for
native PGP pinentry unlock early if necessary, before a long validation stage. Reuse the same
host user, GNUPGHOME, gpg-agent and persistent shell/session across coherent commits. An open
shell does not override the agent's maximum cache expiry; request native unlock again when
required. Do not secretly extend global cache policy, disable signing or use unsigned fallback.

Never ask the user to paste a passphrase/private key into chat. Do not place it in scripts,
environment variables, logs, files, fixture containers or tool arguments. Existing native
agent/pinentry is the only unlock route. Keep commit author/signing identity unchanged.

Suggested coherent checkpoints (combine adjacent ones when appropriate): current input and
original text target fix; text authoring; Files/direct interaction; generated/summary/transcript
family; final validation/documentation. Avoid hundreds of cosmetic commits. Separate native
semantic fixes from mechanical extraction where that makes review and rollback meaningful.

If the new sealed bundle is supplied as untracked history, include that exact input at its
own or first appropriate checkpoint. Do not mutate evidence templates inside it. Do not stage
unrelated user files or private credentials/fixtures. Verify every new signed commit using the
existing git verification tooling and record SHA, scope and verification result.

No push, merge, tag, release, package publication or historical-bundle deletion is authorized.
A needed sibling edit uses that sibling's own coherent signed checkpoint and is reported with
its delivery state. Verify actual local trees/assemblies/assets before claiming source-pair
closure; main-only commits cannot deliver missing sibling APIs.
