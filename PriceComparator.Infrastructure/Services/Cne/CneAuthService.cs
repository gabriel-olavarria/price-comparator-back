
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using System.Diagnostics;
using System.Net.Http;
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

    private static readonly TimeSpan SemaphoreTimeout =
        TimeSpan.FromSeconds(15);

    private static readonly TimeSpan HttpTimeout =
        TimeSpan.FromSeconds(20);

    private static readonly TimeSpan TokenExpirationMargin =
        TimeSpan.FromMinutes(5);

    private static readonly TimeSpan MaxCacheDuration =
        TimeSpan.FromMinutes(55);

    public async Task<string> GetToken(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (TryGetCachedToken(out var cachedToken))
        {
            logger.LogDebug("Se utiliza token CNE almacenado en caché.");

            return cachedToken!;
        }

        logger.LogInformation(
            "Token CNE no encontrado en caché. Esperando semáforo.");

        var stopwatch = Stopwatch.StartNew();

        bool acquired;

        try
        {
            acquired = await TokenSemaphore.WaitAsync(
                SemaphoreTimeout,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Se canceló la espera del semáforo CNE después de {ElapsedMs} ms.",
                stopwatch.ElapsedMilliseconds);

            throw;
        }

        if (!acquired)
        {
            logger.LogError(
                "Timeout esperando semáforo CNE después de {ElapsedMs} ms.",
                stopwatch.ElapsedMilliseconds);

            throw new TimeoutException(
                "No fue posible adquirir el semáforo de autenticación CNE.");
        }

        logger.LogInformation(
            "Semáforo CNE adquirido después de {ElapsedMs} ms.",
            stopwatch.ElapsedMilliseconds);

        try
        {
            // Otra solicitud pudo obtener el token
            // mientras esta esperaba el semáforo.
            if (TryGetCachedToken(out cachedToken))
            {
                logger.LogDebug(
                    "Token CNE encontrado en caché después de adquirir el semáforo.");

                return cachedToken!;
            }

            return await RequestTokenAsync(cancellationToken);
        }
        finally
        {
            TokenSemaphore.Release();

            logger.LogDebug("Semáforo CNE liberado.");
        }
    }

    private bool TryGetCachedToken(out string? token)
    {
        return memoryCache.TryGetValue(TokenCacheKey, out token)
               && !string.IsNullOrWhiteSpace(token);
    }

    private async Task<string> RequestTokenAsync(
        CancellationToken cancellationToken)
    {
        var email = configuration["Cne:Email"];
        var password = configuration["Cne:Password"];
        var loginUrl = configuration["Cne:LoginUrl"];

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(loginUrl))
        {
            throw new InvalidOperationException(
                "Falta configurar la autenticación de CNE.");
        }

        if (!Uri.TryCreate(
                loginUrl,
                UriKind.Absolute,
                out var loginUri) ||
            loginUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Cne:LoginUrl debe ser una URL HTTPS válida.");
        }

        var client = httpClientFactory.CreateClient();

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["email"] = email,
                ["password"] = password
            });

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutCts.CancelAfter(HttpTimeout);

        logger.LogInformation(
            "Iniciando autenticación con CNE.");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await client.PostAsync(
                loginUri,
                content,
                timeoutCts.Token);

            logger.LogInformation(
                "CNE respondió a la autenticación con HTTP {StatusCode} después de {ElapsedMs} ms.",
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(
                timeoutCts.Token);

            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty(
                    "token",
                    out var tokenElement) ||
                tokenElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    "CNE no devolvió un token válido.");
            }

            var token = tokenElement.GetString();

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    "El token de CNE está vacío.");
            }

            var expiration = GetJwtExpiration(token);

            if (!expiration.HasValue)
            {
                throw new InvalidOperationException(
                    "No se pudo determinar la expiración del token CNE.");
            }

            var cacheDuration =
                expiration.Value
                - DateTimeOffset.UtcNow
                - TokenExpirationMargin;

            if (cacheDuration <= TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    "CNE devolvió un token vencido o próximo a vencer.");
            }

            if (cacheDuration > MaxCacheDuration)
            {
                cacheDuration = MaxCacheDuration;
            }

            memoryCache.Set(
                TokenCacheKey,
                token,
                cacheDuration);

            logger.LogInformation(
                "Token CNE almacenado en caché durante {CacheMinutes:F1} minutos.",
                cacheDuration.TotalMinutes);

            return token;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "El cliente canceló la autenticación CNE después de {ElapsedMs} ms.",
                stopwatch.ElapsedMilliseconds);

            throw;
        }
        catch (OperationCanceledException ex)
            when (timeoutCts.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "Timeout de {TimeoutSeconds} segundos autenticando con CNE.",
                HttpTimeout.TotalSeconds);

            throw new TimeoutException(
                "La autenticación con CNE superó el tiempo máximo permitido.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(
                ex,
                "Error HTTP durante la autenticación CNE.");

            throw;
        }
        catch (JsonException ex)
        {
            logger.LogError(
                ex,
                "CNE devolvió una respuesta JSON inválida.");

            throw;
        }
    }

    private static DateTimeOffset? GetJwtExpiration(string token)
    {
        try
        {
            var parts = token.Split('.');

            if (parts.Length != 3)
            {
                return null;
            }

            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');

            payload = payload.PadRight(
                (payload.Length + 3) / 4 * 4,
                '=');

            var bytes = Convert.FromBase64String(payload);

            using var document = JsonDocument.Parse(bytes);

            if (!document.RootElement.TryGetProperty(
                    "exp",
                    out var expirationElement) ||
                !expirationElement.TryGetInt64(
                    out var expirationSeconds))
            {
                return null;
            }

            return DateTimeOffset.FromUnixTimeSeconds(
                expirationSeconds);
        }
        catch (Exception ex)
            when (ex is FormatException
                  or JsonException
                  or ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
