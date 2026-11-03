using System.Reflection;
using Microsoft.EntityFrameworkCore;
using PriceComparator.Domain.Common;

namespace PriceComparator.Infrastructure.Persistence.Configurations;

public static class EntityRegistration
{
    public static void RegisterEntities(this ModelBuilder modelBuilder, Assembly entitiesAssembly, Assembly configurationsAssembly)
    {
        var entityTypes = entitiesAssembly.GetTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                InheritsFromEntity(type));

        foreach (var entityType in entityTypes)
        {
            modelBuilder.Entity(entityType);
        }

        modelBuilder.ApplyConfigurationsFromAssembly(
            configurationsAssembly);
    }

    private static bool InheritsFromEntity(Type type)
    {
        for (var current = type.BaseType;
             current is not null;
             current = current.BaseType)
        {
            if (current.IsGenericType &&
                current.GetGenericTypeDefinition() == typeof(Entity<>))
            {
                return true;
            }
        }

        return false;
    }
}