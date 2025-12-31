namespace B2B.Infrastructure.Messaging;

/// <summary>
/// Configuration options for RabbitMQ connection.
/// </summary>
public class RabbitMqOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "RabbitMQ";

    /// <summary>
    /// Gets or sets the RabbitMQ host address.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the RabbitMQ port.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Gets or sets the virtual host.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string Username { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the password.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the retry count for message processing.
    /// </summary>
    public int RetryCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets the initial retry interval in seconds.
    /// </summary>
    public int RetryIntervalSeconds { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum retry interval in seconds.
    /// </summary>
    public int MaxRetryIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the prefetch count for consumers.
    /// </summary>
    public int PrefetchCount { get; set; } = 16;

    /// <summary>
    /// Gets or sets the concurrent message limit.
    /// </summary>
    public int ConcurrentMessageLimit { get; set; } = 8;

    /// <summary>
    /// Gets the connection string for RabbitMQ.
    /// </summary>
    public string ConnectionString => $"amqp://{Username}:{Password}@{Host}:{Port}{VirtualHost}";
}
