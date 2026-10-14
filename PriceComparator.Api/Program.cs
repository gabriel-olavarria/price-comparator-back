using PriceComparator.Application;
using PriceComparator.Application.Interfaces;
using PriceComparator.Infrastructure;
using PriceComparator.Infrastructure.ProductOffers.Jumbo;
using PriceComparator.Infrastructure.ProductOffers.Lider;
using PriceComparator.Infrastructure.ProductOffers.SantaIsabel;
using PriceComparator.Infrastructure.ProductOffers.Tottus;
using PriceComparator.Infrastructure.ProductOffers.Unimarc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure();


// ======================================================
// DIAGNÓSTICO 1:
// Interfaces implementadas por cada ProductOfferSearcher
// ======================================================

Console.WriteLine();
Console.WriteLine("===== MARKERS DE LOS SEARCHERS =====");

var searcherTypes = new[]
{
    typeof(UnimarcProductOfferSearcher),
    typeof(TottusProductOfferSearcher),
    typeof(SantaIsabelProductOfferSearcher),
    typeof(LiderProductOfferSearcher),
    typeof(JumboProductOfferSearcher)
};

foreach (var type in searcherTypes)
{
    Console.WriteLine();
    Console.WriteLine(type.Name);

    foreach (var @interface in type.GetInterfaces())
    {
        Console.WriteLine($"  -> {@interface.FullName}");
    }

    var implementsTransient = typeof(ITransientDependency).IsAssignableFrom(type);
    var implementsScoped = typeof(IScopedDependency).IsAssignableFrom(type);
    var implementsSingleton = typeof(ISingletonDependency).IsAssignableFrom(type);

    Console.WriteLine($"  ITransientDependency: {implementsTransient}");
    Console.WriteLine($"  IScopedDependency: {implementsScoped}");
    Console.WriteLine($"  ISingletonDependency: {implementsSingleton}");
}

Console.WriteLine();
Console.WriteLine("====================================");


// ======================================================
// DIAGNÓSTICO 2:
// Assembly real cargado para Infrastructure
// ======================================================

Console.WriteLine();
Console.WriteLine("===== ASSEMBLY DE INFRASTRUCTURE =====");

var infrastructureAssembly = typeof(TottusProductOfferSearcher).Assembly;

Console.WriteLine($"Nombre: {infrastructureAssembly.FullName}");
Console.WriteLine($"Location: {infrastructureAssembly.Location}");
Console.WriteLine($"Tottus type: {typeof(TottusProductOfferSearcher).FullName}");
Console.WriteLine($"Tottus implementa ITransientDependency: " + $"{typeof(ITransientDependency).IsAssignableFrom(typeof(TottusProductOfferSearcher))}");
Console.WriteLine("======================================");


// ======================================================
// DIAGNÓSTICO 3:
// Assemblies de tipos e interfaces
// ======================================================

Console.WriteLine();
Console.WriteLine("===== DIAGNÓSTICO ASSEMBLIES =====");

var tottusType = typeof(TottusProductOfferSearcher);
var transientType = typeof(ITransientDependency);
var searcherInterfaceType = typeof(IProductOfferSearcher);

Console.WriteLine();
Console.WriteLine("TottusProductOfferSearcher:");
Console.WriteLine($"  FullName: {tottusType.FullName}");
Console.WriteLine($"  Assembly: {tottusType.Assembly.FullName}");
Console.WriteLine($"  Location: {tottusType.Assembly.Location}");
Console.WriteLine();
Console.WriteLine("IProductOfferSearcher:");
Console.WriteLine($"  FullName: {searcherInterfaceType.FullName}");
Console.WriteLine($"  Assembly: {searcherInterfaceType.Assembly.FullName}");
Console.WriteLine($"  Location: {searcherInterfaceType.Assembly.Location}");
Console.WriteLine();
Console.WriteLine("ITransientDependency:");
Console.WriteLine($"  FullName: {transientType.FullName}");
Console.WriteLine($"  Assembly: {transientType.Assembly.FullName}");
Console.WriteLine($"  Location: {transientType.Assembly.Location}");
Console.WriteLine();
Console.WriteLine("Interfaces que implementa Tottus:");

foreach (var interfaceType in tottusType.GetInterfaces())
{
    Console.WriteLine($"  -> {interfaceType.FullName}");
    Console.WriteLine($"     Assembly: {interfaceType.Assembly.FullName}");
    Console.WriteLine($"     Location: {interfaceType.Assembly.Location}");
}


Console.WriteLine();

var productSearcherInheritsTransient = transientType.IsAssignableFrom(searcherInterfaceType);
var tottusImplementsTransient = transientType.IsAssignableFrom(tottusType);

Console.WriteLine($"IProductOfferSearcher hereda ITransientDependency: " + $"{productSearcherInheritsTransient}");
Console.WriteLine($"Tottus implementa ITransientDependency: " + $"{tottusImplementsTransient}");
Console.WriteLine("==================================");


// ======================================================
// DIAGNÓSTICO 4:
// Cantidad de IProductOfferSearcher registrados en DI
// ======================================================

Console.WriteLine();
Console.WriteLine("===== IProductOfferSearcher REGISTRADOS =====");

var searcherDescriptors = builder.Services
    .Where(x => x.ServiceType == typeof(IProductOfferSearcher))
    .ToList();

Console.WriteLine($"TOTAL REGISTRADOS: {searcherDescriptors.Count}");

foreach (var descriptor in searcherDescriptors)
{
    Console.WriteLine($"ServiceType: {descriptor.ServiceType.Name} | " +
                      $"ImplementationType: " +
                      $"{descriptor.ImplementationType?.FullName ?? "null"} | " +
                      $"ImplementationFactory: " +
                      $"{(descriptor.ImplementationFactory is not null ? "SI" : "NO")} | " +
                      $"Lifetime: {descriptor.Lifetime}");
}

Console.WriteLine("=============================================");
Console.WriteLine();


// ======================================================
// CORS
// ======================================================

const string FrontendCorsPolicy = "FrontendCorsPolicy";

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
        {
            policy.SetIsOriginAllowed(origin =>
                {
                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    {
                        return false;
                    }
                    return uri.Host == "localhost";
                }).AllowAnyHeader()
                .AllowAnyMethod();
        });
});


// ======================================================
// APP
// ======================================================

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(FrontendCorsPolicy);
app.UseAuthorization();
app.MapControllers();
app.Run();