# Administration UI and fresh-client handoff

All three apps run `candoitall-shared-providers-ui:admin-dialogs-20260827-2`.
Image ID: `sha256:180d49fe05f38bebf8aa501c5625ae6c8c6e194ca5d68fdfc116ee39667633cd`.

| Role | Browser URL | State |
| --- | --- | --- |
| Shared source | http://localhost:5210/agents?tab=providers | Real provider catalogs retained |
| Existing client | http://localhost:5212/agents?tab=providers | Existing imports/history retained; test JWT renewed |
| Fresh manual client | http://localhost:5214/agents?tab=providers | Zero providers, connections, imports, user secrets and tokens |

All ports are loopback-only. Ordinary local browser UI needs no bearer token. The API
still requires JWT. The source's upstream credentials never go to either client.

## Connect 5214 manually

1. Open http://localhost:5210/settings?tab=api-access. Give the token a descriptive
   Subject/Display name. Use the button beside Scopes to select **Discover shared providers**
   and **Use shared providers**, then **Use selection**. Exact scopes are
   `api.shared-providers.catalog.read api.shared-providers.invoke`.
2. Create the token and copy it privately. On http://localhost:5214/settings?tab=secrets,
   save it as an **ApiKey**, scope `workspace`, name `Shared instance JWT`.
3. In 5214 Providers, use the **Shared provider connections** icon beside New and Refresh.
   If the newly saved secret is not listed, refresh the provider list first.
   Choose **Add source**, enter name `Shared instance`, base URL
   `http://candoitall-spui-shared:8080/`, and the saved credential. Keep Enabled checked.
   Check **Allow HTTP on a private network** for this isolated Docker setup.
4. Save source, Test, Discover and import; select the desired published providers and
   Apply selection. No local provider definition is required first.
5. Create your Simple Chat at http://localhost:5214/agents?tab=simple-chats.

Use the Docker hostname above in the connection, NOT localhost:5210: container-local
localhost would refer to the client itself. Use only the instance root; the app appends
catalog/relay paths. JWTs expire; renew them and replace the existing client secret value.

**Manage tokens** loads metadata only when its dialog opens. It supports search and
25-row pages. Revoke retains a record but denies new API requests. Delete removes the
record and also denies requests. Tokens issued before tracking was introduced have no
recoverable history and remain subject to their original expiry; the UI explains this.
The 5212 test token is now tracked under `Shared desktop client 5212 (UI)` and has an
eight-hour lifetime from this deployment's UI renewal. Revoking it disconnects 5212.

## Recoverable 5214 reset

- Active database/owner: `candoitall_e2e_fresh_client`.
- Active volume: `candoitall-spui-fresh_app-data-reset-20260827`.
- Retained previous database: `candoitall_e2e_fresh_before_admin_20260827`.
- Retained previous data volume: `candoitall-spui-fresh_app-data`.
- Existing credentials volume is reused for DB bootstrap/signing configuration only;
  it contains no configured upstream providers. New app data/vault/token registry are empty.
- Source and existing-client databases were not reset. 5032 was not touched.

The existing fresh-client compose file now points at the rebuilt image and empty data
volume: `../04-avatar-and-fresh-client/compose.yaml`. `docker compose -f <that file> up -d`
starts it; `stop` preserves data. Do not run `down -v`. The one-time Reset-5214.ps1 refuses
to overwrite existing recovery targets. To recover old state, stop 5214 and explicitly
select the retained database AND old data volume together; preserve the new state first.

All /health endpoints returned 200 Healthy. 5214 Docker health is healthy. DB grants
deny fresh-to-source and source-to-fresh connection, and anonymous source catalog access
from Docker DNS returns 401 as expected. See proof/SB06/runtime-final.txt and
proof/SB06/runtime-image2-final.txt for the final image and repeated empty-data check.
