using CanDoItAll.SharedKernel;
using CanDoItAll.Modules.LlmChats.Common;

namespace CanDoItAll.Modules.LlmChats.Application;

internal static class LlmChatErrors
{
    public static Error InvalidRequest(string message)
        => Error.Validation(message, LlmChatErrorCodes.InvalidRequest);

    public static Error DefinitionNotFound()
        => Error.Failure("The LLM Chat definition was not found.", LlmChatErrorCodes.DefinitionNotFound);

    public static Error DefinitionConcurrencyConflict()
        => Error.Failure(
            "The LLM Chat definition changed after it was read.",
            LlmChatErrorCodes.DefinitionConcurrencyConflict);

    public static Error DefinitionNotActive(string message = "The LLM Chat definition is not active.")
        => Error.Failure(message, LlmChatErrorCodes.DefinitionNotActive);

    public static Error ConversationNotFound()
        => Error.Failure("The LLM Chat conversation was not found.", LlmChatErrorCodes.ConversationNotFound);

    public static Error ConversationArchived()
        => Error.Failure("The LLM Chat conversation is archived and read-only.", LlmChatErrorCodes.ConversationArchived);

    public static Error ActiveTurnConflict()
        => Error.Failure(
            "The LLM Chat conversation has an active or nonterminal turn.",
            LlmChatErrorCodes.ActiveTurnConflict);

    public static Error OperationNotFound()
        => Error.Failure("The LLM Chat operation was not found.", LlmChatErrorCodes.OperationNotFound);

    public static Error OperationIdConflict()
        => Error.Failure(
            "The LLM Chat operation id was reused for a different request.",
            LlmChatErrorCodes.OperationIdConflict);

    public static Error OperationRecoveryRequired()
        => Error.Failure(
            "The exact LLM Chat active turn requires recovery before it can be abandoned.",
            LlmChatErrorCodes.OperationRecoveryRequired);

    public static Error StorageCorrupted()
        => Error.Failure("Required LLM Chat state is missing or inconsistent.", LlmChatErrorCodes.StorageCorrupted);

    public static Error OperationFailure(string code)
        => Error.Failure("The LLM Chat turn could not be completed.", code);
}
