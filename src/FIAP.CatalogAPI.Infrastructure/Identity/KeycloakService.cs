using System.Text.Json;
using FIAP.CatalogAPI.Application.DTOs.Auth;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace FIAP.CatalogAPI.Infrastructure.Identity;

public class KeycloakService : IKeycloakService
{
    private readonly HttpClient _http;
    private readonly KeycloakSettings _settings;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public KeycloakService(HttpClient http, IOptions<KeycloakSettings> options)
    {
        _http = http;
        _settings = options.Value;
    }

    public async Task<LoginOutputDto> GetTokenAsync(string username, string password)
    {
        var tokenUrl = $"{_settings.BaseUrl}realms/{_settings.Realm}/protocol/openid-connect/token";

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", _settings.ClientId),
            new KeyValuePair<string, string>("client_secret", _settings.ClientSecret),
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password),
        });

        var response = await _http.PostAsync(tokenUrl, content);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new UnauthorizedAccessException("Invalid username or password");

        return JsonSerializer.Deserialize<LoginOutputDto>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Keycloak token response");
    }

    public async Task<LoginOutputDto> RefreshTokenAsync(string refreshToken)
    {
        var tokenUrl = $"{_settings.BaseUrl}realms/{_settings.Realm}/protocol/openid-connect/token";

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", _settings.ClientId),
            new KeyValuePair<string, string>("client_secret", _settings.ClientSecret),
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
        });

        var response = await _http.PostAsync(tokenUrl, content);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new UnauthorizedAccessException("Invalid refresh token");

        return JsonSerializer.Deserialize<LoginOutputDto>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Keycloak token response");
    }
}
