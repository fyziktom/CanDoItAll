# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| N001 / R001-R005 | `requirements/01-normalized-requirements.md` | `subbundles/01-01-live-process-approval-actions` | `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter ProcessLiveEscalationActionPolicyTests` plus 5032 validation | The observed escalation is a blocked-step recovery case, not an approval. |
