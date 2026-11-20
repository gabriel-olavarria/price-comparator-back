using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using System.Text.Json;
using System.Threading;

namespace PriceComparator.Infrastructure.Services.Cne;

public sealed class CneAuthService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<CneAuthService> logger,
    IMemoryCache memoryCache
) : ICneAuthService, ISingletonDependency
{
    private const string TokenCacheKey = "Cne:AccessToken";
    private static readonly SemaphoreSlim TokenSemaphore = new(1, 1);
    
    public async Task<string> GetToken(CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(TokenCacheKey, out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
        {
            return cachedToken;
        }

        await TokenSemaphore.WaitAsync(cancellationToken);

        try
        {
            if (memoryCache.TryGetValue(TokenCacheKey, out cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
            {
                return cachedToken;
            }

            var email = configuration["Cne:Email"];
            var password = configuration["Cne:Password"];
            var loginUrl = configuration["Cne:LoginUrl"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(loginUrl))
            {
                throw new InvalidOperationException("Falta configurar la autenticación de CNE.");
            }
            var url = $"{loginUrl}?email={Uri.EscapeDataString(email)}" +
                      $"&password={Uri.EscapeDataString(password)}";

            var client = httpClientFactory.CreateClient();

            using var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["email"] = email,
                    ["password"] = password
                });

            using var response = await client.PostAsync(
                loginUrl,
                content,
                cancellationToken);

            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("token", out var tokenElement) ||
                tokenElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException("CNE no devolvió un token válido.");
            }

            var token = tokenElement.GetString();

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("El token de CNE está vacío.");
            }

            var expiration = GetJwtExpiration(token);

            if (!expiration.HasValue)
            {
                throw new InvalidOperationException("No se pudo determinar la expiración del token CNE.");
            }

            var cacheDuration = expiration.Value - DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);

            if (cacheDuration <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("CNE devolvió un token vencido o próximo a vencer.");
            }

            if (cacheDuration > TimeSpan.FromMinutes(55))
            {
                cacheDuration = TimeSpan.FromMinutes(55);
            }

            memoryCache.Set(TokenCacheKey, token, cacheDuration);

            logger.LogInformation("Token CNE almacenado en caché.");

            return token;
        }
        finally
        {
            TokenSemaphore.Release();
        }
    }


    private static DateTimeOffset? GetJwtExpiration(string token)
    {
        try
        {
            var parts = token.Split('.');

            if (parts.Length != 3)
                return null;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');

            var bytes = Convert.FromBase64String(payload);
            using var document = JsonDocument.Parse(bytes);

            if (!document.RootElement.TryGetProperty("exp", out var expirationElement) || !expirationElement.TryGetInt64(out var expirationSeconds))
            {
                return null;
            }

            return DateTimeOffset.FromUnixTimeSeconds(expirationSeconds);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}