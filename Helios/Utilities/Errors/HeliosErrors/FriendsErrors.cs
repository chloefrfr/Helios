namespace Helios.Utilities.Errors.HeliosErrors;

public static class FriendsErrors
{
    private const string Service = "com.helios.friends";

    public static ApiError SelfFriend => new(
        "errors.com.epicgames.friends.selfFriend",
        "Self friend", Service, 14001, 400);

    public static ApiError AccountNotFound => new(
        "errors.com.epicgames.friends.accountNotFound",
        "Account missing", Service, 14011, 404);

    public static ApiError FriendshipNotFound => new(
        "errors.com.epicgames.friends.friendshipNotFound",
        "Friendship missing", Service, 14004, 404);

    public static ApiError RequestAlreadySent => new(
        "errors.com.epicgames.friends.requestAlreadySent",
        "Duplicate request", Service, 14004, 400);

    public static ApiError InvalidData => new(
        "errors.com.epicgames.friends.invalidData",
        "Invalid data", Service, 14015, 400);
}