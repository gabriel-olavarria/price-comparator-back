using Microsoft.Extensions.Configuration;
using PriceComparator.Application.Interfaces.DependencyInjection;

public sealed class ProductStorageOptions : ISingletonDependency
{
    public bool SaveRaw { get; }
    public bool SaveProcessed { get; }

    public ProductStorageOptions(IConfiguration configuration)
    {
        SaveRaw = configuration.GetValue<bool>("ProductStorage:SaveRaw");
        SaveProcessed = configuration.GetValue<bool>("ProductStorage:SaveProcessed");
    }
}