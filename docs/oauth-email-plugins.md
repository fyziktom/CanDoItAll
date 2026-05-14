# OAuth email plugins

This covers the first Gmail and Office365 mail plugins that use the generic plugin OAuth flow.

## Generic flow

- A plugin declares an `OAuth2` descriptor and one or more OAuth connection descriptors.
- The settings UI starts authorization through `/api/plugins/{pluginId}/oauth/start`.
- The browser returns to `/api/plugins/oauth/callback`.
- OAuth state and PKCE verifier data are stored as vault references. Access and refresh tokens are stored only in the secret vault under a vault key referenced by `Plugins_OAuthConnections`.
- Workflow executors request an access token through `IPluginOAuth2Capability` or `PluginOAuthService`; they never read token material from plugin connection settings.

## Gmail setup

Official references:

- [Google OAuth 2.0 for desktop apps](https://developers.google.com/identity/protocols/oauth2/native-app)
- [Gmail API scopes](https://developers.google.com/workspace/gmail/api/auth/scopes)
- [Gmail users.messages.modify](https://developers.google.com/workspace/gmail/api/reference/rest/v1/users.messages/modify)
- [Gmail users.labels.create](https://developers.google.com/workspace/gmail/api/reference/rest/v1/users.labels/create)

Use the existing Google client:

- Client id: `977924573657-li0lctr50h2mq7p7rue9rfr53cgc1ev5.apps.googleusercontent.com`
- Client secret environment variable: `CANDOITALL_GMAIL_SECRET`. The app checks the process, Windows user, and Windows machine environment scopes in that order.
- Required scope: `https://www.googleapis.com/auth/gmail.modify`

Steps:

1. In Google Cloud Console, make sure the Gmail API is enabled for the `candoitall` project.
2. Configure the OAuth consent screen. For local testing, keep the app in testing mode and add your Google account as a test user.
3. Add the Gmail modify scope to the consent screen. The workflow needs it because Gmail label mutation uses `users.messages.modify`, and the processed label may be created with `users.labels.create`.
4. Use an OAuth client of type `Desktop app`. Google recommends the loopback redirect pattern for Windows desktop apps.
5. Start CanDoItAll from a shell where `CANDOITALL_GMAIL_SECRET` is set.
6. In Plugins, install and enable `Gmail Mail`, grant `WorkflowExecutor` and `OAuth2`, then click Login.
7. For the workflow test, create or choose a Gmail label named `CanDoItAllSummaryTest` and apply it to one message.
8. Run the `gmail-label-email-summary-to-project` workflow. When its Gmail executor `connectionId` settings are empty, the workflow automatically uses the latest enabled connected Gmail OAuth connection with the required scopes.
9. After the workflow stores the summary asset, it adds `CanDoItAllSummaryTestProcessed` to the processed message and removes `CanDoItAllSummaryTest`.

Existing Gmail connections created with the old readonly scope show `ReconnectRequired`; click Login again so Google grants `https://www.googleapis.com/auth/gmail.modify`. Disconnect is optional.

If Google returns `redirect_uri_mismatch`, use the exact running callback URL `http://localhost:{port}/api/plugins/oauth/callback` in a web OAuth client, or configure the Gmail plugin connection `redirectUri` setting to match the registered redirect.

## Office365 setup

Official references:

- [Microsoft identity platform authorization code flow](https://learn.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-auth-code-flow)
- [Microsoft Graph list messages](https://learn.microsoft.com/en-us/graph/api/user-list-messages?view=graph-rest-1.0)
- [Microsoft Graph query parameters](https://learn.microsoft.com/en-us/graph/query-parameters)

Steps:

1. Register an app in Microsoft Entra admin center.
2. Configure a public client redirect URI for the local callback, for example `http://localhost:{port}/api/plugins/oauth/callback`.
3. Add delegated Microsoft Graph permission `Mail.Read`. The plugin also requests `openid` and `offline_access` so the Microsoft identity platform can complete sign-in and issue refresh tokens.
4. In the Office365 plugin connection settings, set `clientId` to the app registration client id. Set `redirectUri` only when you need to force an exact callback URL.
5. In Plugins, install and enable `Office365 Mail`, grant `WorkflowExecutor` and `OAuth2`, then click Login. OAuth login opens in a separate browser tab and asks Microsoft for consent again when reconnecting.
6. For the workflow test, create or choose an Outlook category named `CanDoItAllSummaryTest` and assign it to a small set of messages.
7. Run the `office365-category-email-summary-to-project` workflow. When its Office365 executor `connectionId` setting is empty, the workflow automatically uses the latest enabled connected Office365 OAuth connection with the required scopes.

## Example workflows

The default workflow template pack includes:

- `gmail-label-email-summary-to-project`: downloads one Gmail message by label, summarizes it with the workflow LLM component, stores markdown through the project-structure executor, then moves the message from `CanDoItAllSummaryTest` to `CanDoItAllSummaryTestProcessed`.
- `office365-category-email-summary-to-project`: downloads a bounded Office365 category batch through Microsoft Graph, summarizes it, and stores markdown through the project-structure executor.

Both templates require a real OAuth login. The `connectionId` settings can stay empty unless you need to pin a workflow to a specific OAuth connection.
