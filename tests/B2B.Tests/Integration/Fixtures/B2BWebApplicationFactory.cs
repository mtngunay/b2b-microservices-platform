using B2B.Application.Interfaces.Persistence;
using B2B.Application.Interfaces.Services;
using B2B.Infrastructure.Persistence.ReadDb;
using B2B.Infrastructure.Persistence.ReadDb.ReadModels;
using B2B.Infrastructure.Persistence.WriteDb;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using StackExchange.Redis;
using Testcontainers.MsSql;
using Testcontainers.MongoDb;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace B2B.Tests.Integration.Fixtures;

/// <summary>
/// Custom WebApplicationFactory for integration testing with TestContainers.
/// Provides isolated database instances for each test run.
/// </summary>
public class B2BWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private MsSqlContainer? _msSqlContainer;
    private MongoDbContainer? _mongoDbContainer;
    private RedisContainer? _redisContainer;
    private RabbitMqContainer? _rabbitMqContainer;

    public string MsSqlConnectionString => _msSqlContainer?.GetConnectionString() ?? string.Empty;
    public string MongoDbConnectionString => _mongoDbContainer?.GetConnectionString() ?? string.Empty;
    public string RedisConnectionString => _redisContainer?.GetConnectionString() ?? string.Empty;
    public string RabbitMqConnectionString => _rabbitMqContainer?.GetConnectionString() ?? string.Empty;

    public async Task InitializeAsync()
    {
        // Start all containers in parallel
        _msSqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("Test@Password123!")
            .Build();

        _mongoDbContainer = new MongoDbBuilder()
            .WithImage("mongo:7.0")
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7.2-alpine")
            .Build();

        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.12-management-alpine")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

        await Task.WhenAll(
            _msSqlContainer.StartAsync(),
            _mongoDbContainer.StartAsync(),
            _redisContainer.StartAsync(),
            _rabbitMqContainer.StartAsync());
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

        if (_msSqlContainer != null)
            await _msSqlContainer.DisposeAsync();

        if (_mongoDbContainer != null)
            await _mongoDbContainer.DisposeAsync();

        if (_redisContainer != null)
            await _redisContainer.DisposeAsync();

        if (_rabbitMqContainer != null)
            await _rabbitMqContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remove existing database registrations
            services.RemoveAll<DbContextOptions<WriteDbContext>>();
            services.RemoveAll<WriteDbContext>();
            services.RemoveAll<IMongoClient>();
            services.RemoveAll<MongoDbContext>();
            services.RemoveAll<IConnectionMultiplexer>();

            // Configure MSSQL (Write Database)
            services.AddDbContext<WriteDbContext>(options =>
            {
                options.UseSqlServer(MsSqlConnectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null);
                });
            });

            // Configure MongoDB (Read Database)
            services.Configure<MongoDbSettings>(options =>
            {
                var mongoUrl = new MongoUrl(MongoDbConnectionString);
                options.ConnectionString = MongoDbConnectionString;
                options.DatabaseName = mongoUrl.DatabaseName ?? "B2B_Test";
            });

            services.AddSingleton<IMongoClient>(sp =>
            {
                return new MongoClient(MongoDbConnectionString);
            });

            services.AddScoped<MongoDbContext>();

            // Configure Redis
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configOptions = ConfigurationOptions.Parse(RedisConnectionString);
                configOptions.AbortOnConnectFail = false;
                configOptions.ConnectRetry = 3;
                configOptions.ConnectTimeout = 5000;
                configOptions.AllowAdmin = true; // Required for FLUSHDB in tests
                return ConnectionMultiplexer.Connect(configOptions);
            });

            // Register IApplicationDbContext
            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<WriteDbContext>());
        });
    }

    /// <summary>
    /// Creates and migrates the database schema.
    /// </summary>
    public async Task EnsureDatabaseCreatedAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    /// <summary>
    /// Resets the database to a clean state.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        
        // Delete all data from tables
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM UserPermissions");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM UserRoles");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM RolePermissions");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Users");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Roles");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Permissions");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Tenants");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM OutboxMessages");

        // Clear MongoDB collections
        var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        await mongoContext.Users.DeleteManyAsync(Builders<UserReadModel>.Filter.Empty);
        await mongoContext.Orders.DeleteManyAsync(Builders<OrderReadModel>.Filter.Empty);

        // Clear Redis
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var server = redis.GetServer(redis.GetEndPoints().First());
        await server.FlushDatabaseAsync();
    }
}
