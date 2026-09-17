using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UserService.Tests;

public class AuthControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid()}@test.com";

    [Fact]
    public async Task Register_ValidRequest_Returns201WithPatronRole()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = UniqueEmail(),
            password = "Test123!@#",
            firstName = "Test",
            lastName = "User",
            phoneNumber = "+1-555-0100"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(body);
        Assert.Equal("PATRON", body!.Role);
        Assert.Equal("ACTIVE", body.MembershipStatus);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        var email = UniqueEmail();
        var payload = new
        {
            email,
            password = "Test123!@#",
            firstName = "Test",
            lastName = "User",
            phoneNumber = "+1-555-0100"
        };

        await _client.PostAsJsonAsync("/api/auth/register", payload);
        var secondResponse = await _client.PostAsJsonAsync("/api/auth/register", payload);

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);

        var body = await secondResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("VALIDATION_ERROR", body!.Error);
    }

    [Theory]
    [InlineData("short1!")]
    [InlineData("nouppercase1!")]
    [InlineData("NOLOWERCASE1!")]
    [InlineData("NoDigitsHere!")]
    [InlineData("NoSpecialChar123")]
    public async Task Register_WeakPassword_Returns400(string weakPassword)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = UniqueEmail(),
            password = weakPassword,
            firstName = "Test",
            lastName = "User",
            phoneNumber = "+1-555-0100"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenWithUserInfo()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Test123!@#",
            firstName = "Test",
            lastName = "User",
            phoneNumber = "+1-555-0100"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Test123!@#"
        });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.Equal("Bearer", body!.TokenType);
        Assert.Equal(86400, body.ExpiresIn);
        Assert.False(string.IsNullOrEmpty(body.AccessToken));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Test123!@#",
            firstName = "Test",
            lastName = "User",
            phoneNumber = "+1-555-0100"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPassword1!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_NonExistentEmail_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = UniqueEmail(),
            password = "Test123!@#"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Validate_ExistingUser_ReturnsUserInfo()
    {
        var email = UniqueEmail();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Test123!@#",
            firstName = "Test",
            lastName = "User",
            phoneNumber = "+1-555-0100"
        });
        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();

        var response = await _client.GetAsync($"/api/users/{registered!.UserId}/validate");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ValidateResponse>();
        Assert.Equal(email, body!.Email);
    }

    [Fact]
    public async Task Validate_NonExistentUser_Returns404()
    {
        var response = await _client.GetAsync($"/api/users/{Guid.NewGuid()}/validate");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private class RegisterResponse
    {
        public Guid UserId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string MembershipStatus { get; set; } = string.Empty;
    }

    private class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
    }

    private class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    private class ValidateResponse
    {
        public string Email { get; set; } = string.Empty;
    }
}