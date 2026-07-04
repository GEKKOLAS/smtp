namespace MailTemplateHub.Domain.Errors;

/// <summary>Catalog of machine-readable error codes exposed via the API (spec 06-api.md).</summary>
public static class ErrorCodes
{
    public static class Send
    {
        public const string InvalidTransition = "send.invalid_transition";
    }
}
