using System.Net;
using System.Net.Http.Json;
using B2B.Application.DTOs;
using B2B.Tests.Integration.Fixtures;
using FluentAssertions;

namespace B2B.Tests.Integration;

/// <summary>
/// Integration tests for Users controller (CQRS flow).
/// </summary>
[Collection("Integration")]
public class UsersControllerTests : IntegrationTestBase
{
    public UsersControllerTests(B2BWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetUsers_WithValidToken_ReturnsPagedResult()
    {
        // Arrange
        var adminEmail = "admin@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: adminEmail, password: password);
        await CreateTestUserAsync(email: "user1@example.com", password: password);
        await CreateTestUserAsync(email: "user2@example.com", password: password);

        var tokenResult = await AuthenticateAsync(adminEmail, password);
        SetAuthorizationHeader(tokenResult!.AccessToken);

        // Act
        var response = await Client.GetAsync("/api/users?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCountGreaterOrEqualTo(3);
        result.TotalCount.Should().BeGreaterOrEqualTo(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetUsers_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthorizationHeader();

        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUsers_WithSearchFilter_ReturnsFilteredResults()
    {
        // Arrange
        var adminEmail = "search-admin@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: adminEmail, password: password);
        await CreateTestUserAsync(email: "searchable@example.com", password: password, firstName: "Searchable");
        await CreateTestUserAsync(email: "other@example.com", password: password, firstName: "Other");

        var tokenResult = await AuthenticateAsync(adminEmail, password);
        SetAuthorizationHeader(tokenResult!.AccessToken);

        // Act
        var response = await Client.GetAsync("/api/users?search=searchable");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().Contain(u => u.Email.Contains("searchable") || u.FirstName.Contains("Searchable"));
    }

    [Fact]
    public async Task GetUserById_WithValidId_ReturnsUser()
    {
        // Arrange
        var adminEmail = "getbyid-admin@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: adminEmail, password: password);
        var targetUser = await CreateTestUserAsync(
            email: "target@example.com", 
            password: password, 
            firstName: "Target", 
            lastName: "User");

        var tokenResult = await AuthenticateAsync(adminEmail, password);
        SetAuthorizationHeader(tokenResult!.AccessToken);

        // Act
        var response = await Client.GetAsync($"/api/users/{targetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        user.Should().NotBeNull();
        user!.Id.Should().Be(targetUser.Id);
        user.Email.Should().Be("target@example.com");
        user.FirstName.Should().Be("Target");
        user.LastName.Should().Be("User");
    }

    [Fact]
    public async Task GetUserById_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var adminEmail = "notfound-admin@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: adminEmail, password: password);

        var tokenResult = await AuthenticateAsync(adminEmail, password);
        SetAuthorizationHeader(tokenResult!.AccessToken);

        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/users/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateUser_WithValidData_ReturnsCreatedUser()
    {
        // Arrange
        var adminEmail = "create-admin@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: adminEmail, password: password);

        var tokenResult = await AuthenticateAsync(adminEmail, password);
        SetAuthorizationHeader(tokenResult!.AccessToken);

        var createRequest = new CreateUserRequest
        {
            Email = "newuser@example.com",
            Password = "NewUser@123",
            FirstName = "New",
            LastName = "User",
            RoleIds = new List<Guid>()
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/users", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdUser = await response.Content.ReadFromJsonAsync<UserDto>();
        createdUser.Should().NotBeNull();
        createdUser!.Email.Should().Be("newuser@example.com");
        createdUser.FirstName.Should().Be("New");
        createdUser.LastName.Should().Be("User");
        createdUser.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_ReturnsConflict()
    {
        // Arrange
        var adminEmail = "duplicate-admin@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: adminEmail, password: password);
        await CreateTestUserAsync(email: "existing@example.com", password: password);

        var tokenResult = await AuthenticateAsync(adminEmail, password);
        SetAuthorizationHeader(tokenResult!.AccessToken);

        var createRequest = new CreateUserRequest
        {
            Email = "existing@example.com",
            Password = "Test@123",
            FirstName = "Duplicate",
            LastName = "User",
            RoleIds = new List<Guid>()
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/users", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateUser_WithMissingEmail_ReturnsBadRequest()
    {
        // Arrange
        var adminEmail = "missing-email-admin@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: adminEmail, password: password);

        var tokenResult = await AuthenticateAsync(adminEmail, password);
        SetAuthorizationHeader(tokenResult!.AccessToken);

        var createRequest = new CreateUserRequest
        {
            Email = "",
            Password = "Test@123",
            FirstName = "Test",
            LastName = "User",
            RoleIds = new List<Guid>()
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/users", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateUser_WithValidData_ReturnsUpdatedUser()
    {
        // Arrange
        var adminEmail = "update-admin@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: adminEmail, password: password);
        var targetUser = await CreateTestUserAsync(
            email: "update-target@example.com", 
            password: password,
            firstName: "Original",
            lastName: "Name");

        var tokenResult = await AuthenticateAsync(adminEmail, password);
        SetAuthorizationHeader(tokenResult!.AccessToken);

        var updateRequest = new UpdateUserRequest
        {
            FirstName = "Updated",
            LastName = "Name",
            IsActive = true
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/users/{targetUser.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedUser = await response.Content.ReadFromJsonAsync<UserDto>();
        updatedUser.Should().NotBeNull();
        updatedUser!.FirstName.Should().Be("Updated");
        updatedUser.LastName.Should().Be("Name");
    }

    [Fact]
    public async Task UpdateUser_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var adminEmail = "update-notfound-admin@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: adminEmail, password: password);

        var tokenResult = await AuthenticateAsync(adminEmail, password);
        SetAuthorizationHeader(tokenResult!.AccessToken);

        var updateRequest = new UpdateUserRequest
        {
            FirstName = "Updated",
            LastName = "Name",
            IsActive = true
        };

        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.PutAsJsonAsync($"/api/users/{nonExistentId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var adminEmail = "delete-admin@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: adminEmail, password: password);
        var targetUser = await CreateTestUserAsync(email: "delete-target@example.com", password: password);

        var tokenResult = await AuthenticateAsync(adminEmail, password);
        SetAuthorizationHeader(tokenResult!.AccessToken);

        // Act
        var response = await Client.DeleteAsync($"/api/users/{targetUser.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify user is soft deleted (not found in subsequent query)
        var getResponse = await Client.GetAsync($"/api/users/{targetUser.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var adminEmail = "delete-notfound-admin@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: adminEmail, password: password);

        var tokenResult = await AuthenticateAsync(adminEmail, password);
        SetAuthorizationHeader(tokenResult!.AccessToken);

        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/api/users/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUsers_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var adminEmail = "pagination-admin@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: adminEmail, password: password);

        // Create multiple users
        for (int i = 1; i <= 5; i++)
        {
            await CreateTestUserAsync(email: $"page-user{i}@example.com", password: password);
        }

        var tokenResult = await AuthenticateAsync(adminEmail, password);
        SetAuthorizationHeader(tokenResult!.AccessToken);

        // Act
        var response = await Client.GetAsync("/api/users?page=1&pageSize=3");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(3);
        result.TotalCount.Should().BeGreaterOrEqualTo(6);
        result.HasNextPage.Should().BeTrue();
    }
}
