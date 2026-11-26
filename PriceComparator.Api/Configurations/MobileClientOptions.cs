namespace PriceComparator.Api.Configurations;

public sealed class MobileClientOptions
{
    public const string SectionName = "Mobile";
    public const string ApiKeyHeaderName = "X-Mobile-Api-Key";

    public string ApiKey { get; set; } = string.Empty;
}
