using Hangfire.Dashboard;

namespace B2B.Worker.Filters;

/// <summary>
/// Authorization filter for Hangfire dashboard.
/// Implements basic authentication for dashboard access.
/// </summary>
public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly string _username;
    private readonly string _password;

    /// <summary>
    /// Initializes a new instance of HangfireDashboardAuthorizationFilter.
    /// </summary>
    /// <param name="username">Dashboard username.</param>
    /// <param name="password">Dashboard password.</param>
    public HangfireDashboardAuthorizationFilter(string username, string password)
    {
        _username = username ?? throw new ArgumentNullException(nameof(username));
        _password = password ?? throw new ArgumentNullException(nameof(password));
    }

    /// <summary>
    /// Authorizes access to the Hangfire dashboard.
    /// </summary>
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Check for Basic Authentication header
        var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            SetUnauthorizedResponse(httpContext);
            return false;
        }

        try
        {
            var encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
            var decodedCredentials = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(encodedCredentials));
            var credentials = decodedCredentials.Split(':', 2);

            if (credentials.Length != 2)
            {
                SetUnauthorizedResponse(httpContext);
                return false;
            }

            var username = credentials[0];
            var password = credentials[1];

            if (username == _username && password == _password)
            {
                return true;
            }

            SetUnauthorizedResponse(httpContext);
            return false;
        }
        catch
        {
            SetUnauthorizedResponse(httpContext);
            return false;
        }
    }

    /// <summary>
    /// Sets the 401 Unauthorized response with WWW-Authenticate header.
    /// </summary>
    private static void SetUnauthorizedResponse(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = 401;
        httpContext.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire Dashboard\"";
    }
}
