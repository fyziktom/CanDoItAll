# Target component map

Names are suggestions, not a requirement to create all types.

## Neutral project

- `Presentation/ConversationPresentationKey.cs`
- `Presentation/PresentationBadge.cs`
- `Presentation/ConversationParticipantPresentation.cs`
- `Presentation/ConversationThreadPresentation.cs`
- `Presentation/ConversationMessagePresentation.cs`
- `Participants/ConversationParticipantCard.razor`
- `Participants/ConversationParticipantCompactList.razor`
- `Participants/ConversationParticipantCompactListItem.razor`
- `Participants/ConversationParticipantPicker.razor`
- `Threads/ConversationThreadRail.razor`
- `Threads/ConversationThreadListItem.razor`
- `Threads/ConversationThreadHistoryDialog.razor`
- `Workspace/ConversationWorkspacePanel.razor`
- `Workspace/ConversationHeader.razor`
- `Workspace/ConversationTranscript.razor`
- `Workspace/ConversationMessageBubble.razor`
- `Workspace/ConversationComposer.razor`
- `Workspace/ConversationPromptTextArea.razor`
- `Workspace/ConversationMarkdownRenderer.cs`
- `Settings/ConversationDefinitionEditorShell.razor`
- `Settings/ConversationIdentityFields.razor`
- `Settings/ConversationProviderModelFields.razor`
- `Settings/ConversationTemperatureField.razor`
- `Floating/ConversationFloatingCatalog.razor`
- `Floating/ConversationLifecycleSettingsFields.razor`

Create fewer types when a smaller cohesive design satisfies ownership. Do not collapse all families into one universal component.

## Agent adapters/facades

- `AgentConversationPresentationMapper` or focused mappers per family;
- current Agent components delegating to neutral owners;
- agent-only slot components for execution, approvals, voice, attachments, context, runtime details;
- current product code-behind retaining service calls and commands.

Do not centralize all mappings and side effects in one new `AgentChatUiService`.
