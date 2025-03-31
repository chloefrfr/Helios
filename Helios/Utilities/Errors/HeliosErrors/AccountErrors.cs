namespace Helios.Utilities.Errors.HeliosErrors;

public static class AccountErrors
{
    private const string Service = "com.helios.account";

    public static ApiError DisabledAccount => new(
        "errors.com.epicgames.account.disabledAccount",
        "Account disabled", Service, 18001, 403);

    public static ApiError InactiveAccount => new(
        "errors.com.epicgames.account.account_not_active",
        "Account inactive", Service, -1, 400);

    public static ApiError InvalidAccountIdCount => new(
        "errors.com.epicgames.account.invalidAccountIdCount",
        "Invalid ID count", Service, 18066, 400);

    public static ApiError AccountNotFound(string accountId) => new(
        "errors.com.epicgames.account.accountNotFound",
        "Account not found: {0}", Service, 18007, 404, accountId);
}