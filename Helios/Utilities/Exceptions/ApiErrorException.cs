using Helios.Utilities.Errors;

namespace Helios.Utilities.Exceptions;

public class ApiErrorException : Exception
{
    public ApiError Error { get; }

    public ApiErrorException(ApiError error) : base(error.Response.ErrorMessage)
    {
        Error = error;
    }
}