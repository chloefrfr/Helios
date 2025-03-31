using Helios.Utilities.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Utilities.Extensions;

public static class HttpContextExtensions
{
    public static IActionResult ApplyApiError(this HttpContext context, ApiError error)
    {
        return error.Apply(context);
    }
}