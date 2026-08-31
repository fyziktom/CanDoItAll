# Finishing request — 2026-08-31

The user explicitly requested rebuilding/restarting the existing 5032 application and Docker publisher/client, testing the project PDF-versus-Excel comparison through real agent tool calls, testing a complex task and simple chat through shared providers, inspecting publisher request records, repairing any failure, and keeping all three updated applications running for manual pre-merge testing.

The request initially identifies Docker ports 5210 and 5214 and later says 5120. Live container inspection resolves the publisher to 5210 and client to 5214; no 5120 listener exists. Preserve these live ports, databases, files, provider configuration and secrets. Do not run the destructive three-application fixture reset or touch unrelated 8080 services.

This authorizes these bounded live inference requests and adoption of the repaired build on the existing profiles. It supersedes the earlier disposable-5032/restore-old-app proposal for this finishing lane. Source starts at committed aadd953150e7f659e4060ced6505621c705ea61f on providers-shared. The previous frozen Stable pass remains historical proof; do not rerun that broad suite without a new invalidation trigger.

The original deterministic three-application lifecycle and independent review remain separately identified requirements; do not relabel this two-Docker-instance acceptance as those checks.
