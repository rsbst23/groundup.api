namespace GroundUp.Core
{
    public static class ErrorCodes
    {
        public const string NotFound = "ERR_NOT_FOUND";
        public const string ValidationFailed = "ERR_VALIDATION_FAILED";
        public const string DuplicateEntry = "ERR_DUPLICATE_ENTRY";
        public const string Conflict = "ERR_CONFLICT";
        public const string Unauthorized = "ERR_UNAUTHORIZED";
        public const string InternalServerError = "ERR_INTERNAL_SERVER_ERROR";
        public const string IdMismatch = "ERR_ID_MISMATCH";
        public const string UnhandledException = "ERR_UnhandledException";
        public const string InvalidCredentials = "ERR_INVALID_CREDENTIALS";
        public const string RegistrationFailed = "ERR_REGISTRATION_FAILED";
        public const string UserNotFound = "ERR_USER_NOT_FOUND";
        public const string Forbidden = "ERR_FORBIDDEN";
    }
}
