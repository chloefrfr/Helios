namespace Helios.Utilities.Errors.HeliosErrors;

public static class MCPErrors
{
    private const string Service = "com.helios.mcp";

        public static ApiError ProfileNotFound(string profileId) => new(
            "errors.com.epicgames.mcp.profileNotFound",
            "Profile not found: {0}", Service, 18007, 404, profileId);

        public static ApiError EmptyItems => new(
            "errors.com.epicgames.mcp.emptyItems",
            "No items found", Service, 12700, 404);

        public static ApiError NotEnoughMtx(string item, int required, int balance) => new(
            "errors.com.epicgames.mcp.notEnoughMtx",
            "Insufficient MTX for {0}: {1}/{2}", Service, 12720, 400, 
            item, required.ToString(), balance.ToString());

        public static ApiError WrongCommand => new(
            "errors.com.epicgames.mcp.wrongCommand",
            "Invalid command", Service, 12801, 400);

        public static ApiError OperationForbidden => new(
            "errors.com.epicgames.mcp.operationForbidden",
            "Operation forbidden", Service, 12813, 403);

        public static ApiError TemplateNotFound => new(
            "errors.com.epicgames.mcp.templateNotFound",
            "Template missing", Service, 12813, 404);

        public static ApiError InvalidHeader => new(
            "errors.com.epicgames.mcp.invalidHeader",
            "Invalid header", Service, 12831, 400);

        public static ApiError InvalidPayload => new(
            "errors.com.epicgames.mcp.invalidPayload",
            "Invalid payload", Service, 12806, 400);

        public static ApiError ItemNotFound => new(
            "errors.com.epicgames.mcp.itemNotFound",
            "Item missing", Service, 16006, 404);

        public static ApiError WrongItemType(string itemId, string expectedType) => new(
            "errors.com.epicgames.mcp.wrongItemType",
            "Invalid type for {0}: {1}", Service, 16009, 400, itemId, expectedType);

        public static ApiError InvalidChatRequest => new(
            "errors.com.epicgames.mcp.invalidChatRequest",
            "Invalid chat", Service, 16090, 400);

        public static ApiError OperationNotFound => new(
            "errors.com.epicgames.mcp.operationNotFound",
            "Operation not found", Service, 16035, 404);

        public static ApiError InvalidLockerSlotIndex(int index) => new(
            "errors.com.epicgames.mcp.InvalidLockerSlotIndex",
            "Invalid loadout slot index {0}", Service, 16173, 400, index.ToString());

        public static ApiError OutOfBounds(int source, int target) => new(
            "errors.com.epicgames.mcp.outOfBounds",
            "Invalid indices {0}-{1}", Service, 16026, 400, 
            source.ToString(), target.ToString());
}