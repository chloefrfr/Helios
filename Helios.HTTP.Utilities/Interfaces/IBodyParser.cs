using Microsoft.AspNetCore.Http;

namespace Helios.HTTP.Utilities.Interfaces;

public interface IBodyParser
{
    Task<IReadOnlyDictionary<string, string>> ParseAsync(HttpRequest request, CancellationToken cancellationToken = default);
}