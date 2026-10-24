using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceComparator.Application.Interfaces.Services;

namespace PriceComparator.Infrastructure.Security;

public sealed class TurnstileVerificationService(
    HttpClient httpClient,
    IOptions<TurnstileOptions> options,
    ILogger<TurnstileVerificationService> logger)
    : ITurnstileVerificationService
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public async Task<bool> VerifyAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("Turnstile: token vacío o ausente.");
            return false;
        }

        // Identificador del token para detectar reutilizaciones.
        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token))
        );

        logger.LogInformation(
            "Turnstile: TokenHash={TokenHash}",
            tokenHash
        );

        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.SecretKey) || string.IsNullOrWhiteSpace(settings.AllowedHostname) || string.IsNullOrWhiteSpace(settings.ExpectedAction))
        {
            logger.LogError("Turnstile: configuración incompleta. SecretKey configurada: {HasSecretKey}", !string.IsNullOrWhiteSpace(settings.SecretKey));

            return false;
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = settings.SecretKey,
                ["response"] = token
            });

        using var response = await httpClient.PostAsync(VerifyUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Turnstile: Cloudflare respondió HTTP {StatusCode}", (int)response.StatusCode);

            return false;
        }

        var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken);

        if (result is null)
        {
            logger.LogWarning("Turnstile: respuesta vacía de Cloudflare.");
            return false;
        }

        logger.LogInformation("Turnstile: Success={Success}, Hostname={Hostname}, Action={Action}, Errors={Errors}", result.Success, result.Hostname, result.Action, string.Join(", ", result.ErrorCodes ?? []));

        var validHostname = string.Equals(result.Hostname, settings.AllowedHostname, StringComparison.OrdinalIgnoreCase);
        var validAction = string.Equals(result.Action, settings.ExpectedAction, StringComparison.Ordinal);

        if (!result.Success || !validHostname || !validAction)
        {
            logger.LogWarning("Turnstile rechazado: Success={Success}, HostnameValido={ValidHostname}, ActionValida={ValidAction}", result.Success, validHostname, validAction);
            return false;
        }
        return true;
    }

    private sealed class TurnstileResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}