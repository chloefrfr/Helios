namespace Helios.Utilities.Errors.HeliosErrors;

public static class StorefrontErrors
{
    private const string Service = "com.helios.storefront";
        
    public static ApiError InvalidItem(params string[] vars) => new(
        "errors.com.epicgames.fortnite.invalid_item_id",
        "Failed to get item from the current shop.",
        Service, 1040, 400, vars);

    public static ApiError CurrencyInsufficient => new(
        "errors.com.epicgames.currency.mtx.insufficient",
        "You cannot afford this item.", 
        Service, 1040, 400);

    public static ApiError HasAllItems => new(
        "errors.com.epicgames.offer.has_all_items",
        "You already own every item.",
        Service, 1040, 400);

    public static ApiError AlreadyOwned => new(
        "errors.com.epicgames.offer.already_owned",
        "You already own this item.",
        Service, 1040, 400);
}