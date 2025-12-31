using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using B2B.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace B2B.Infrastructure.Messaging.Resilience;

/// <summary>
/// Service for categorizing exceptions and extracting structured stack trace information.
/// Supports configurable exception-to-category mappings.
/// </summary>
public class ExceptionCategorizer : IExceptionCategorizer
{
    private readonly ILogger<ExceptionCategorizer> _logger;
    private readonly ConcurrentDictionary<Type, ExceptionCategory> _categoryMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionCategorizer"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ExceptionCategorizer(ILogger<ExceptionCategorizer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _categoryMap = new ConcurrentDictionary<Type, ExceptionCategory>();
        InitializeDefaultMappings();
    }

    /// <summary>
    /// Initializes default exception type to category mappings.
    /// </summary>
    private void InitializeDefaultMappings()
    {
        // Transient exceptions - may succeed on retry
        _categoryMap[typeof(TimeoutException)] = ExceptionCategory.Transient;
        _categoryMap[typeof(OperationCanceledException)] = ExceptionCategory.Transient;
        _categoryMap[typeof(TaskCanceledException)] = ExceptionCategory.Transient;
        _categoryMap[typeof(HttpRequestException)] = ExceptionCategory.Transient;
        _categoryMap[typeof(SocketException)] = ExceptionCategory.Transient;
        _categoryMap[typeof(IOException)] = ExceptionCategory.Transient;

        // Validation exceptions - should not retry
        _categoryMap[typeof(ValidationException)] = ExceptionCategory.Validation;
        _categoryMap[typeof(ArgumentException)] = ExceptionCategory.Validation;
        _categoryMap[typeof(ArgumentNullException)] = ExceptionCategory.Validation;
        _categoryMap[typeof(ArgumentOutOfRangeException)] = ExceptionCategory.Validation;
        _categoryMap[typeof(FormatException)] = ExceptionCategory.Validation;

        // Security exceptions - should not retry
        _categoryMap[typeof(UnauthorizedException)] = ExceptionCategory.Security;
        _categoryMap[typeof(ForbiddenException)] = ExceptionCategory.Security;
        _categoryMap[typeof(UnauthorizedAccessException)] = ExceptionCategory.Security;
        _categoryMap[typeof(System.Security.SecurityException)] = ExceptionCategory.Security;

        // Business exceptions - should not retry
        _categoryMap[typeof(DomainException)] = ExceptionCategory.Business;
        _categoryMap[typeof(NotFoundException)] = ExceptionCategory.Business;
        _categoryMap[typeof(ConflictException)] = ExceptionCategory.Business;
        _categoryMap[typeof(InvalidOperationException)] = ExceptionCategory.Business;

        // Infrastructure exceptions - may retry with backoff
        _categoryMap[typeof(NotSupportedException)] = ExceptionCategory.Infrastructure;
        _categoryMap[typeof(ObjectDisposedException)] = ExceptionCategory.Infrastructure;
    }

    /// <inheritdoc />
    public ExceptionCategory Categorize(Exception exception)
    {
        if (exception == null)
            return ExceptionCategory.Unknown;

        var exceptionType = exception.GetType();

        // Check exact type match first
        if (_categoryMap.TryGetValue(exceptionType, out var category))
        {
            _logger.LogDebug(
                "Exception {ExceptionType} categorized as {Category} (exact match)",
                exceptionType.Name,
                category);
            return category;
        }

        // Check base types and interfaces
        foreach (var (type, cat) in _categoryMap)
        {
            if (type.IsAssignableFrom(exceptionType))
            {
                _logger.LogDebug(
                    "Exception {ExceptionType} categorized as {Category} (base type match: {BaseType})",
                    exceptionType.Name,
                    cat,
                    type.Name);
                return cat;
            }
        }

        // Check exception message for common patterns
        var messageCategory = CategorizeByMessage(exception);
        if (messageCategory != ExceptionCategory.Unknown)
        {
            _logger.LogDebug(
                "Exception {ExceptionType} categorized as {Category} (message pattern match)",
                exceptionType.Name,
                messageCategory);
            return messageCategory;
        }

        _logger.LogDebug(
            "Exception {ExceptionType} categorized as Unknown (no match found)",
            exceptionType.Name);
        return ExceptionCategory.Unknown;
    }


    /// <summary>
    /// Attempts to categorize exception based on message patterns.
    /// </summary>
    private static ExceptionCategory CategorizeByMessage(Exception exception)
    {
        var message = exception.Message.ToLowerInvariant();

        // Transient patterns
        if (message.Contains("timeout") || 
            message.Contains("connection") ||
            message.Contains("network") ||
            message.Contains("temporarily unavailable") ||
            message.Contains("retry"))
        {
            return ExceptionCategory.Transient;
        }

        // Infrastructure patterns
        if (message.Contains("database") ||
            message.Contains("sql") ||
            message.Contains("mongodb") ||
            message.Contains("redis") ||
            message.Contains("rabbitmq") ||
            message.Contains("connection string"))
        {
            return ExceptionCategory.Infrastructure;
        }

        // Security patterns
        if (message.Contains("unauthorized") ||
            message.Contains("forbidden") ||
            message.Contains("access denied") ||
            message.Contains("authentication") ||
            message.Contains("permission"))
        {
            return ExceptionCategory.Security;
        }

        // Validation patterns
        if (message.Contains("validation") ||
            message.Contains("invalid") ||
            message.Contains("required") ||
            message.Contains("must be"))
        {
            return ExceptionCategory.Validation;
        }

        return ExceptionCategory.Unknown;
    }

    /// <inheritdoc />
    public StackTraceInfo ExtractStackTraceInfo(Exception exception)
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));

        var stackTrace = new StackTrace(exception, true);
        var frame = GetRelevantStackFrame(stackTrace);

        var info = new StackTraceInfo
        {
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Category = Categorize(exception),
            Message = exception.Message,
            Source = exception.Source ?? string.Empty,
            Method = frame?.GetMethod()?.Name ?? string.Empty,
            DeclaringType = frame?.GetMethod()?.DeclaringType?.FullName ?? string.Empty,
            LineNumber = frame?.GetFileLineNumber() ?? 0,
            FilePath = frame?.GetFileName() ?? string.Empty,
            StackTrace = exception.StackTrace ?? string.Empty,
            ExceptionFingerprint = ComputeFingerprint(exception),
            CapturedAt = DateTime.UtcNow
        };

        // Extract additional data from exception
        ExtractAdditionalData(exception, info);

        // Extract inner exceptions recursively
        ExtractInnerExceptions(exception, info);

        return info;
    }


    /// <summary>
    /// Gets the most relevant stack frame (first frame with source info).
    /// </summary>
    private static StackFrame? GetRelevantStackFrame(StackTrace stackTrace)
    {
        var frames = stackTrace.GetFrames();
        if (frames == null || frames.Length == 0)
            return null;

        // Try to find a frame with line number info
        var frameWithLineInfo = frames.FirstOrDefault(f => f.GetFileLineNumber() > 0);
        if (frameWithLineInfo != null)
            return frameWithLineInfo;

        // Fall back to first frame
        return frames.FirstOrDefault();
    }

    /// <summary>
    /// Extracts additional data from exception's Data dictionary.
    /// </summary>
    private static void ExtractAdditionalData(Exception exception, StackTraceInfo info)
    {
        if (exception.Data.Count == 0)
            return;

        foreach (var key in exception.Data.Keys)
        {
            if (key == null)
                continue;
                
            var keyStr = key.ToString();
            var valueStr = exception.Data[key]?.ToString();
            
            if (!string.IsNullOrEmpty(keyStr) && valueStr != null)
            {
                info.AdditionalData[keyStr] = valueStr;
            }
        }
    }

    /// <summary>
    /// Extracts inner exceptions recursively.
    /// </summary>
    private void ExtractInnerExceptions(Exception exception, StackTraceInfo info)
    {
        // Handle regular inner exception
        if (exception.InnerException != null)
        {
            info.InnerExceptions.Add(ExtractStackTraceInfo(exception.InnerException));
        }

        // Handle AggregateException specially
        if (exception is AggregateException aggEx)
        {
            foreach (var innerEx in aggEx.InnerExceptions)
            {
                // Avoid duplicates (InnerException is usually the first inner exception)
                if (innerEx != exception.InnerException)
                {
                    info.InnerExceptions.Add(ExtractStackTraceInfo(innerEx));
                }
            }
        }
    }

    /// <summary>
    /// Computes a fingerprint hash for grouping similar exceptions.
    /// </summary>
    private static string ComputeFingerprint(Exception exception)
    {
        var components = new StringBuilder();
        
        // Include exception type
        components.Append(exception.GetType().FullName);
        components.Append(':');
        
        // Include target site (method where exception was thrown)
        if (exception.TargetSite != null)
        {
            components.Append(exception.TargetSite.DeclaringType?.FullName);
            components.Append('.');
            components.Append(exception.TargetSite.Name);
        }
        components.Append(':');
        
        // Include first line of stack trace for more specificity
        var stackTrace = exception.StackTrace;
        if (!string.IsNullOrEmpty(stackTrace))
        {
            var firstLine = stackTrace.Split('\n').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstLine))
            {
                components.Append(firstLine.GetHashCode());
            }
        }

        // Compute SHA256 hash and return first 16 characters
        var inputBytes = Encoding.UTF8.GetBytes(components.ToString());
        var hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes)[..16];
    }


    /// <inheritdoc />
    public bool IsRetryable(Exception exception)
    {
        var category = Categorize(exception);
        
        // Only transient and some infrastructure exceptions are retryable
        return category switch
        {
            ExceptionCategory.Transient => true,
            ExceptionCategory.Infrastructure => true, // May succeed with backoff
            ExceptionCategory.Business => false,
            ExceptionCategory.Validation => false,
            ExceptionCategory.Security => false,
            ExceptionCategory.Unknown => true, // Default to retry for unknown
            _ => false
        };
    }

    /// <inheritdoc />
    public void RegisterExceptionCategory<TException>(ExceptionCategory category) 
        where TException : Exception
    {
        var exceptionType = typeof(TException);
        _categoryMap[exceptionType] = category;
        
        _logger.LogInformation(
            "Registered custom exception category mapping: {ExceptionType} -> {Category}",
            exceptionType.Name,
            category);
    }

    /// <summary>
    /// Registers multiple exception types to a category.
    /// </summary>
    /// <param name="category">The category to assign.</param>
    /// <param name="exceptionTypes">The exception types to register.</param>
    public void RegisterExceptionCategories(ExceptionCategory category, params Type[] exceptionTypes)
    {
        foreach (var type in exceptionTypes)
        {
            if (!typeof(Exception).IsAssignableFrom(type))
            {
                _logger.LogWarning(
                    "Type {Type} is not an Exception type, skipping registration",
                    type.Name);
                continue;
            }

            _categoryMap[type] = category;
            _logger.LogDebug(
                "Registered exception category mapping: {ExceptionType} -> {Category}",
                type.Name,
                category);
        }
    }

    /// <summary>
    /// Gets all registered exception category mappings.
    /// </summary>
    /// <returns>Dictionary of exception types to categories.</returns>
    public IReadOnlyDictionary<Type, ExceptionCategory> GetRegisteredMappings()
    {
        return _categoryMap;
    }
}
