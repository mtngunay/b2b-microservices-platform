using B2B.Application.Interfaces.Services;

namespace B2B.Infrastructure.Identity;

/// <summary>
/// Provides access to the current correlation ID using AsyncLocal storage.
/// Thread-safe implementation for distributed tracing across async operations.
/// </summary>
public class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    /// <summary>
    /// Gets the current correlation ID. Returns a new GUID if not set.
    /// </summary>
    public string CorrelationId => _correlationId.Value ?? Guid.NewGuid().ToString();

    /// <summary>
    /// Sets the correlation ID for the current async context.
    /// </summary>
    /// <param name="correlationId">The correlation ID to set.</param>
    public void SetCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Correlation ID cannot be null or empty.", nameof(correlationId));
        }

        _correlationId.Value = correlationId;
    }

    /// <summary>
    /// Clears the correlation ID from the current context.
    /// </summary>
    public void Clear()
    {
        _correlationId.Value = null;
    }
}
