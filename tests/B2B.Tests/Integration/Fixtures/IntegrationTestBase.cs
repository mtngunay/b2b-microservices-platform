using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using B2B.Application.DTOs;
using B2B.Domain.Aggregates;
using B2B.Domain.Entities;
using B2B.Infrastructure.Persistence.ReadDb;
using B2B.Infrastructure.Persistence.ReadDb.ReadModels;
using B2B.Infrastructure.Persistence.WriteDb;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Tests.Integration.Fixtures;

/// <summary>
/// Base class for integration tests providing common setup and utilities.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<B2BWebApplicationFactory>, IAsyncLifetime
{
    protected readonly B2BWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(B2BWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    public virtual async Task InitializeAsync()
    {
        await Factory.EnsureDatabaseCreatedAsync();
        await Factory.ResetDatabaseAsync();
    }

    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a test user in the database.
    /// </summary>
    protected async Task<User> CreateTestUserAsync(
        string email = "test@example.com",
        string password = "Test@123",
        string firstName = "Test",
        string lastName = "User",
        string tenantId = "test-tenant",
        bool isActive = true)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WriteDbContext>();

        var passwordHash = ComputeHash(password);
        var user = User.Create(email, passwordHash, firstName, lastName, tenantId);

        if (!isActive)
        {
            user.Deactivate();
        }

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Also create in MongoDB read model
        var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        var userReadModel = new UserReadModel
        {
            Id = user.Id.ToString(),
            TenantId = tenantId,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            FullName = $"{firstName} {lastName}",
            IsActive = isActive,
            IsDeleted = false,
            Roles = new List<string>(),
            Permissions = new List<string>(),
            CreatedAt = DateTime.UtcNow
        };
        await mongoContext.Users.InsertOneAsync(userReadModel);

        return user;
    }

    /// <summary>
    /// Creates a test role in the database.
    /// </summary>
    protected async Task<Role> CreateTestRoleAsync(
        string name = "TestRole",
        string description = "Test Role Description",
        string tenantId = "test-tenant")
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WriteDbContext>();

        var role = Role.Create(name, description, tenantId);
        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();

        return role;
    }

    /// <summary>
    /// Creates a test tenant in the database.
    /// </summary>
    protected async Task<Tenant> CreateTestTenantAsync(
        string name = "Test Tenant",
        string subdomain = "test",
        string contactEmail = "admin@test.com")
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WriteDbContext>();

        var tenant = Tenant.Create(name, subdomain, contactEmail);
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        return tenant;
    }

    /// <summary>
    /// Authenticates a user and returns the token result.
    /// </summary>
    protected async Task<TokenResult?> AuthenticateAsync(string email, string password)
    {
        var loginRequest = new LoginRequest { Email = email, Password = password };
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<TokenResult>();
    }

    /// <summary>
    /// Sets the authorization header with the given token.
    /// </summary>
    protected void SetAuthorizationHeader(string token)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Clears the authorization header.
    /// </summary>
    protected void ClearAuthorizationHeader()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Computes SHA256 hash of a password.
    /// </summary>
    protected static string ComputeHash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
