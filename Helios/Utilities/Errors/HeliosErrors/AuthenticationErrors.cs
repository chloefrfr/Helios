namespace Helios.Utilities.Errors.HeliosErrors;

public static class AuthenticationErrors
{
    private const string Service = "com.helios.auth";

    public static ApiError InvalidHeader => new(
        "errors.com.epicgames.authentication.invalidHeader",
        "Invalid authorization header", Service, 1011, 400);

    public static ApiError MissingPermission(string permission) => new(
        "errors.com.epicgames.auth.missingPermission",
        "Missing permission: {0}", Service, 12806, 403, permission);

    public static ApiError InvalidRequest => new(
        "errors.com.epicgames.authentication.invalidRequest",
        "Invalid request body", Service, 1013, 400);

    public static ApiError InvalidToken(string token) => new(
        "errors.com.epicgames.authentication.invalidToken",
        "Invalid token: {0}", Service, 1014, 401, token);

    public static ApiError WrongGrantType => new(
        "errors.com.epicgames.authentication.wrongGrantType",
        "Invalid grant type", Service, 1016, 400);

    public static ApiError NotYourAccount => new(
        "errors.com.epicgames.authentication.notYourAccount",
        "Unauthorized account access", Service, 1023, 403);

    public static ApiError ValidationFailed(string token) => new(
        "errors.com.epicgames.authentication.validationFailed",
        "Token validation failed: {0}", Service, 1031, 401, token);

    public static ApiError AuthenticationFailed(string service) => new(
        "errors.com.epicgames.authentication.authenticationFailed",
        "Auth failed for {0}", Service, 1032, 401, service);

    public static ApiError NotOwnSessionRemoval(string sessionId) => new(
        "errors.com.epicgames.authentication.notOwnSessionRemoval",
        "Cannot remove session {0}", Service, 18040, 403, sessionId);

    public static ApiError UnknownSession(string sessionId) => new(
        "errors.com.epicgames.authentication.unknownSession",
        "Unknown session {0}", Service, 18051, 404, sessionId);

    public static ApiError UsedClientToken => new(
        "errors.com.epicgames.authentication.wrongTokenType",
        "Invalid token type", Service, 18052, 401);

    public static class OAuth
    {
        public static ApiError InvalidBody => new(
            "errors.com.epicgames.authentication.oauth.invalidBody",
            "Invalid OAuth body", Service, 1013, 400);

        public static ApiError UnsupportedGrant(string grantType) => new(
            "errors.com.epicgames.common.oauth.unsupported_grant_type",
            "Unsupported grant: {0}", Service, 1016, 400, grantType);

        public static ApiError InvalidExternalAuth(string authType) => new(
            "errors.com.epicgames.authentication.oauth.invalidExternalAuthType",
            "Invalid auth type: {0}", Service, 1016, 400, authType);

        public static ApiError GrantNotImplemented(string grantType) => new(
            "errors.com.epicgames.authentication.grantNotImplemented",
            "Grant not implemented: {0}", Service, 1016, 501, grantType);

        public static ApiError TooManySessions => new(
            "errors.com.epicgames.authentication.oauth.tooManySessions",
            "Too many sessions", Service, 18048, 400);

        public static ApiError InvalidAccountCredentials => new(
            "errors.com.epicgames.authentication.oauth.invalidAccountCredentials",
            "Invalid credentials", Service, 18031, 400);

        public static ApiError InvalidRefresh => new(
            "errors.com.epicgames.authentication.oauth.invalidRefresh",
            "Invalid refresh token", Service, 18036, 400);

        public static ApiError InvalidClient => new(
            "errors.com.epicgames.authentication.oauth.invalidClient",
            "Invalid client", Service, 18033, 403);

        public static ApiError InvalidExchange(string code) => new(
            "errors.com.epicgames.authentication.oauth.invalidExchange",
            "Invalid exchange code: {0}", Service, 18057, 400, code);

        public static ApiError ExpiredExchangeCodeSession => new(
            "errors.com.epicgames.authentication.oauth.expiredExchangeCodeSession",
            "Expired exchange session", Service, 18128, 400);

        public static ApiError CorrectiveActionRequired => new(
            "errors.com.epicgames.authentication.oauth.corrective_action_required",
            "Action required", Service, 18206, 400);
    }
}