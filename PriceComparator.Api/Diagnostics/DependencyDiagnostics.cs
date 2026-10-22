using Microsoft.Extensions.DependencyInjection;

namespace PriceComparator.Api.Diagnostics;

public static class DependencyDiagnostics
{
    public static void Validate(IServiceProvider provider, IServiceCollection registrations, ILogger logger)
    {
        using var scope = provider.CreateScope();
        var services = registrations
            .Where(x => x.ServiceType.IsInterface)
            .Where(x => x.ServiceType.Namespace?.StartsWith("PriceComparator") == true)
            .Select(x => x.ServiceType)
            .Distinct()
            .OrderBy(x => x.Name)
            .ToArray();

        logger.LogInformation("Iniciando diagnóstico de {Count} dependencias", services.Length);

        foreach (var serviceType in services)
        {
            logger.LogInformation("Verificando: {Service}", serviceType.Name);

            try
            {
                scope.ServiceProvider.GetRequiredService(serviceType);
                logger.LogInformation("{Service}: OK", serviceType.Name);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "ERROR: {Service}", serviceType.Name);
                throw;
            }
        }

        logger.LogInformation("Diagnóstico de dependencias finalizado correctamente");
    }
}