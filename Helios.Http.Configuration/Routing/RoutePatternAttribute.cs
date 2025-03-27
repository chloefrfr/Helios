using Helios.Http.Configuration.Routing.Enums;

namespace Helios.Http.Configuration.Routing;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class RoutePatternAttribute : Attribute
{
    public string Pattern { get; set; }
    public HttpMethodType Method { get; set; }
    
    /// <summary>
    /// Initializes a new route pattern with optional HTTP method.
    /// Defaults to GET if no method is specified.
    /// </summary>
    /// <param name="pattern">URL route pattern</param>
    /// <param name="httpMethod">HTTP method for the route</param>
    public RoutePatternAttribute(string pattern,
        HttpMethodType method = HttpMethodType.Get)
    {
        Pattern = pattern;
        Method = method;
    }
    
    
}