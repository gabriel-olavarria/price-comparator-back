using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PriceComparator.Api.Configurations;

namespace PriceComparator.Api.Security;

public sealed class MobileClientKeyValidator(IOptions<MobileClientOptions> options)
{
    public bool HasProvidedKey(HttpRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.Headers[MobileClientOptions.ApiKeyHeaderName]);
    }

    public bool IsValid(HttpRequest request)
    {
        var provided = request.Headers[MobileClientOptions.ApiKeyHeaderName].ToString();
        var expected = options.Value.ApiKey;

        if (string.IsNullOrWhiteSpace(provided) || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));

        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }
}
