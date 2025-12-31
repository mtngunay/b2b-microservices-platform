namespace B2B.API.Logging;

/// <summary>
/// Defines log categories for structured logging.
/// Each category represents a specific operation type for filtering in ELK.
/// </summary>
public static class LogCategories
{
    /// <summary>
    /// Category for HTTP GET requests (read operations).
    /// </summary>
    public const string Read = "READ";

    /// <summary>
    /// Category for HTTP POST requests (create operations).
    /// </summary>
    public const string Create = "CREATE";

    /// <summary>
    /// Category for HTTP PUT/PATCH requests (update operations).
    /// </summary>
    public const string Update = "UPDATE";

    /// <summary>
    /// Category for HTTP DELETE requests (delete operations).
    /// </summary>
    public const string Delete = "DELETE";

    /// <summary>
    /// Category for authentication operations.
    /// </summary>
    public const string Auth = "AUTH";

    /// <summary>
    /// Category for system/infrastructure operations.
    /// </summary>
    public const string System = "SYSTEM";

    /// <summary>
    /// Category for event processing operations.
    /// </summary>
    public const string Event = "EVENT";

    /// <summary>
    /// Category for background job operations.
    /// </summary>
    public const string Job = "JOB";

    /// <summary>
    /// Gets the log category based on HTTP method.
    /// </summary>
    public static string GetCategoryFromMethod(string httpMethod)
    {
        return httpMethod.ToUpperInvariant() switch
        {
            "GET" => Read,
            "POST" => Create,
            "PUT" => Update,
            "PATCH" => Update,
            "DELETE" => Delete,
            _ => System
        };
    }
}
