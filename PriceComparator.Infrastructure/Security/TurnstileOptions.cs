namespace PriceComparator.Infrastructure.Security;

public sealed class TurnstileOptions
{
    public const string SectionName = "Turnstile";
    public string SecretKey { get; set; } = string.Empty;
    public string AllowedHostname { get; set; } = string.Empty;
    public string ExpectedAction { get; set; } = string.Empty;
}