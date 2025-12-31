using System.Net;
using System.Net.Http.Json;
using B2B.Application.DTOs;
using B2B.Tests.Integration.Fixtures;
using FluentAssertions;

namespace B2B.Tests.Integration;

/// <summary>
/// Integration tests for authentication endpoints.
/// </summary>
[Collection("Integration")]
public class AuthControllerTests : IntegrationTestBase
{
    public AuthControllerTests(B2BWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        // Arrange
        var email = "auth-test@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: email, password: password);

        var loginRequest = new LoginRequest { Email = email, Password = password };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenResult = await response.Content.ReadFromJsonAsync<TokenResult>();
        tokenResult.Should().NotBeNull();
        tokenResult!.AccessToken.Should().NotBeNullOrEmpty();
        tokenResult.RefreshToken.Should().NotBeNullOrEmpty();
        tokenResult.TokenType.Should().Be("Bearer");
        tokenResult.ExpiresIn.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var email = "auth-invalid@example.com";
        await CreateTestUserAsync(email: email, password: "CorrectPassword");

        var loginRequest = new LoginRequest { Email = email, Password = "WrongPassword" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest 
        { 
            Email = "nonexistent@example.com", 
            Password = "AnyPassword" 
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithInactiveUser_ReturnsUnauthorized()
    {
        // Arrange
        var email = "inactive@example.com";
        await CreateTestUserAsync(email: email, password: "Test@123", isActive: false);

        var loginRequest = new LoginRequest { Email = email, Password = "Test@123" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithEmptyCredentials_ReturnsBadRequest()
    {
        // Arrange
        var loginRequest = new LoginRequest { Email = "", Password = "" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Logout_WithValidToken_ReturnsNoContent()
    {
        // Arrange
        var email = "logout-test@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: email, password: password);

        var tokenResult = await AuthenticateAsync(email, password);
        tokenResult.Should().NotBeNull();

        SetAuthorizationHeader(tokenResult!.AccessToken);

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthorizationHeader();

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithValidRefreshToken_ReturnsNewTokens()
    {
        // Arrange
        var email = "refresh-test@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: email, password: password);

        var tokenResult = await AuthenticateAsync(email, password);
        tokenResult.Should().NotBeNull();

        var refreshRequest = new RefreshTokenRequest { RefreshToken = tokenResult!.RefreshToken };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var newTokenResult = await response.Content.ReadFromJsonAsync<TokenResult>();
        newTokenResult.Should().NotBeNull();
        newTokenResult!.AccessToken.Should().NotBeNullOrEmpty();
        newTokenResult.RefreshToken.Should().NotBeNullOrEmpty();
        // New tokens should be different from old ones (token rotation)
        newTokenResult.AccessToken.Should().NotBe(tokenResult.AccessToken);
        newTokenResult.RefreshToken.Should().NotBe(tokenResult.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithInvalidRefreshToken_ReturnsUnauthorized()
    {
        // Arrange
        var refreshRequest = new RefreshTokenRequest { RefreshToken = "invalid-refresh-token" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithEmptyRefreshToken_ReturnsBadRequest()
    {
        // Arrange
        var refreshRequest = new RefreshTokenRequest { RefreshToken = "" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AccessProtectedEndpoint_AfterLogout_ReturnsUnauthorized()
    {
        // Arrange
        var email = "protected-test@example.com";
        var password = "Test@123";
        await CreateTestUserAsync(email: email, password: password);

        var tokenResult = await AuthenticateAsync(email, password);
        tokenResult.Should().NotBeNull();

        SetAuthorizationHeader(tokenResult!.AccessToken);

        // Logout
        await Client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest());

        // Act - Try to access protected endpoint with revoked token
        var response = await Client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
