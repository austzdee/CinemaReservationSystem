using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CinemaReservation.Tests;

// Exercises authentication and authorization through the real ASP.NET Core
// request pipeline to validate middleware, Identity, JWTs, and role enforcement together.
[Collection("Integration")]
public class AuthIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(
        CustomWebApplicationFactory factory)
    {
        // Use HTTPS in the test client because the application enables
        // HTTPS redirection in its normal request pipeline.
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync(
            "/api/secure/authenticated");

        // Protected routes must reject requests that do not provide
        // a valid authenticated identity.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var request = new
        {
            Email = "unknown@example.com",
            Password = "WrongPassword1!"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            request);

        // Invalid credentials must not produce an authenticated session
        // or reveal whether the supplied email corresponds to an account.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Register_NewUser_ReturnsCreated()
    {
        var email = CreateUniqueEmail();

        var response = await RegisterUserAsync(email);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var email = CreateUniqueEmail();

        var firstResponse = await RegisterUserAsync(email);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        var duplicateResponse = await RegisterUserAsync(email);

        // Identity must prevent multiple accounts from being created
        // for the same application email address.
        Assert.Equal(
            HttpStatusCode.Conflict,
            duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAccessToken()
    {
        var email = CreateUniqueEmail();
        const string password = "Cinema1!";

        var registrationResponse =
            await RegisterUserAsync(email, password);

        Assert.Equal(
            HttpStatusCode.Created,
            registrationResponse.StatusCode);

        var loginResponse =
            await LoginAsync(email, password);

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode);

        var token = await ReadAccessTokenAsync(loginResponse);

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task AuthenticatedEndpoint_WithUserToken_ReturnsOk()
    {
        var email = CreateUniqueEmail();
        const string password = "Cinema1!";

        await RegisterUserAsync(email, password);

        var loginResponse =
            await LoginAsync(email, password);

        var token = await ReadAccessTokenAsync(loginResponse);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/secure/authenticated");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_WithUserToken_ReturnsForbidden()
    {
        var email = CreateUniqueEmail();
        const string password = "Cinema1!";

        await RegisterUserAsync(email, password);

        var loginResponse =
            await LoginAsync(email, password);

        var token = await ReadAccessTokenAsync(loginResponse);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/secure/admin");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await _client.SendAsync(request);

        // Authentication alone is insufficient for privileged routes;
        // the caller must also carry the required Admin role claim.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_WithAdminToken_ReturnsOk()
    {
        var adminPassword =
            Environment.GetEnvironmentVariable(
                "TEST_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "TEST_ADMIN_PASSWORD is not configured.");
        }

        var loginResponse =
            await LoginAsync(
                "admin@example.com",
                adminPassword);

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode);

        var token = await ReadAccessTokenAsync(loginResponse);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/secure/admin");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task PromoteUser_WithAdminToken_AssignsAdminRole()
    {
        var email = CreateUniqueEmail();
        const string password = "Cinema1!";

        var registrationResponse =
            await RegisterUserAsync(email, password);

        Assert.Equal(
            HttpStatusCode.Created,
            registrationResponse.StatusCode);

        var registeredUser =
            await registrationResponse.Content
                .ReadFromJsonAsync<JsonElement>();

        var userId =
            registeredUser.GetProperty("id").GetString()
            ?? throw new InvalidOperationException(
                "Registration response did not contain a user ID.");

        var adminPassword =
            Environment.GetEnvironmentVariable(
                "TEST_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "TEST_ADMIN_PASSWORD is not configured.");
        }

        var adminLoginResponse =
            await LoginAsync(
                "admin@example.com",
                adminPassword);

        Assert.Equal(
            HttpStatusCode.OK,
            adminLoginResponse.StatusCode);

        var adminToken =
            await ReadAccessTokenAsync(
                adminLoginResponse);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/users/{userId}/promote");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var promotionResponse =
            await _client.SendAsync(request);

        // Only an authenticated administrator may elevate another user's
        // privileges through the application-managed role workflow.
        Assert.Equal(
            HttpStatusCode.OK,
            promotionResponse.StatusCode);

        var promotedLoginResponse =
            await LoginAsync(email, password);

        var promotedToken =
            await ReadAccessTokenAsync(
                promotedLoginResponse);

        using var adminAccessRequest =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/secure/admin");

        adminAccessRequest.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                promotedToken);

        var adminAccessResponse =
            await _client.SendAsync(
                adminAccessRequest);

        // A newly issued token must reflect the persisted Admin role
        // after the promotion has completed.
        Assert.Equal(
            HttpStatusCode.OK,
            adminAccessResponse.StatusCode);
    }

    private async Task<HttpResponseMessage> RegisterUserAsync(
        string email,
        string password = "Cinema1!")
    {
        return await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                Email = email,
                Password = password
            });
    }

    private async Task<HttpResponseMessage> LoginAsync(
        string email,
        string password)
    {
        return await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                Email = email,
                Password = password
            });
    }

    private static async Task<string> ReadAccessTokenAsync(
        HttpResponseMessage response)
    {
        var json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        if (!json.TryGetProperty(
                "accessToken",
                out var tokenElement))
        {
            throw new InvalidOperationException(
                "Login response did not contain an access token.");
        }

        return tokenElement.GetString()
            ?? throw new InvalidOperationException(
                "Access token was empty.");
    }

    private static string CreateUniqueEmail()
    {
        return $"integration-{Guid.NewGuid():N}@example.com";
    }
}