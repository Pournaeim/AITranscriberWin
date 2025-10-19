using System;

namespace AITranscriberWinApp.Services
{
    internal static class TranslationErrorFormatter
    {
        public static string BuildUserFacingMessage(Exception exception, bool isRealtime)
        {
            var prefix = isRealtime
                ? "Real-time translation unavailable."
                : "Translation unavailable.";

            var detail = NormalizeExceptionMessage(exception);

            var message = string.IsNullOrWhiteSpace(detail)
                ? prefix
                : prefix + " " + detail;

            if (!ContainsVerificationInstruction(detail))
            {
                message += " Verify the translation service URL or disable translation in Settings if the issue persists.";
            }

            return message.Trim();
        }

        private static string NormalizeExceptionMessage(Exception exception)
        {
            if (exception == null)
            {
                return "An unexpected error occurred.";
            }

            var message = exception.Message?.Trim();
            var innerMessage = exception.InnerException?.Message?.Trim();

            if (!string.IsNullOrWhiteSpace(innerMessage) && !string.Equals(innerMessage, message, StringComparison.Ordinal))
            {
                message = string.IsNullOrWhiteSpace(message)
                    ? innerMessage
                    : message + " (" + innerMessage + ")";
            }

            return string.IsNullOrWhiteSpace(message)
                ? "An unexpected error occurred."
                : message;
        }

        private static bool ContainsVerificationInstruction(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.IndexOf("verify the translation service url", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
