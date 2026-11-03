
using Microsoft.EntityFrameworkCore;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Infrastructure.Persistence.Configurations;

public static class ConceptSeed
{
    public static void Seed(DbContext context)
    {
        var categories = new[]
        {
            "Abarrotes",
            "Mascotas",
            "Bebidas y Licores",
            "Lacteos"
        };

        foreach (var name in categories)
        {
            if (!context.Set<Category>().Any(x => x.Name == name))
            {
                context.Set<Category>().Add(new Category
                {
                    Name = name
                });
            }
        }

        context.SaveChanges();

        var concepts = new (string Name, string Category)[]
        {
            ("Aceite", "Abarrotes"),
            ("Arroz", "Abarrotes"),
            ("Harina", "Abarrotes"),
            ("Pasta", "Abarrotes"),
            ("Leche", "Lacteos"),
            ("Bebida", "Bebidas y Licores")
        };

        foreach (var (name, categoryName) in concepts)
        {
            var category = context.Set<Category>()
                .Single(x => x.Name == categoryName);

            if (!context.Set<Concept>()
                    .Any(x => x.Name == name && x.CategoryId == category.Id))
            {
                context.Set<Concept>().Add(new Concept
                {
                    Name = name,
                    CategoryId = category.Id
                });
            }
        }

        context.SaveChanges();

        var aliases = new (string Name, string Concept)[]
        {
            ("Aceite", "Aceite"),
            ("Aceites", "Aceite"),

            ("Bebida", "Bebida"),
            ("Bebidas", "Bebida"),
            ("Gaseosa", "Bebida"),
            ("Gaseosas", "Bebida"),

            ("Fideos", "Pasta"),
            ("Fideo", "Pasta"),
            ("Tallarin", "Pasta"),
            ("Tallarines", "Pasta"),
            ("Pasta", "Pasta"),
            ("Pastas", "Pasta")
        };

        foreach (var (name, conceptName) in aliases)
        {
            var concept = context.Set<Concept>()
                .Single(x => x.Name == conceptName);

            if (!context.Set<Aliases>()
                    .Any(x => x.Name == name && x.ConceptId == concept.Id))
            {
                context.Set<Aliases>().Add(new Aliases
                {
                    Name = name,
                    ConceptId = concept.Id
                });
            }
        }

        context.SaveChanges();

        var subtypes = new (string Name, string Concept)[]
        {
            ("Oliva", "Aceite"),
            ("Maravilla", "Aceite"),
            ("Vegetal", "Aceite"),
            ("Canola", "Aceite"),

            ("Integral", "Arroz"),
            ("Parbolizado", "Arroz"),
            ("Precocido", "Arroz"),
            ("Preparado", "Arroz"),
            ("Pregraneado", "Arroz"),
            ("Grado 1", "Arroz"),
            ("Grado 2", "Arroz"),
            ("Especial", "Arroz")
        };

        foreach (var (name, conceptName) in subtypes)
        {
            var concept = context.Set<Concept>()
                .Single(x => x.Name == conceptName);

            if (!context.Set<Subtype>()
                    .Any(x => x.Name == name && x.ConceptId == concept.Id))
            {
                context.Set<Subtype>().Add(new Subtype
                {
                    Name = name,
                    ConceptId = concept.Id
                });
            }
        }

        context.SaveChanges();

        var terms = new (string Name, string Subtype, string Concept)[]
        {
            ("Extra Virgen", "Oliva", "Aceite"),
            ("Virgen", "Oliva", "Aceite"),
            ("Oliva", "Oliva", "Aceite"),

            ("g1", "Grado 1", "Arroz"),
            ("grado 1", "Grado 1", "Arroz"),
            ("g2", "Grado 2", "Arroz"),
            ("grado 2", "Grado 2", "Arroz"),

            ("chaufan", "Preparado", "Arroz"),
            ("curry", "Preparado", "Arroz"),
            ("primavera", "Preparado", "Arroz"),
            ("campesino", "Preparado", "Arroz"),
            ("champinon", "Preparado", "Arroz"),
            ("champinones", "Preparado", "Arroz"),
            ("arabe", "Preparado", "Arroz"),
            ("paella", "Preparado", "Arroz"),
            ("sushi", "Preparado", "Arroz"),
            ("wok", "Preparado", "Arroz"),
            ("risotto", "Preparado", "Arroz"),

            ("basmati", "Especial", "Arroz"),
            ("jazmin", "Especial", "Arroz")
        };

        foreach (var (name, subtypeName, conceptName) in terms)
        {
            var subtype = context.Set<Subtype>()
                .Single(x =>
                    x.Name == subtypeName &&
                    x.Concept.Name == conceptName);

            if (!context.Set<Term>()
                    .Any(x => x.Name == name && x.SubtypeId == subtype.Id))
            {
                context.Set<Term>().Add(new Term
                {
                    Name = name,
                    SubtypeId = subtype.Id
                });
            }
        }

        context.SaveChanges();
    }
    
    
    public static Task SeedAsync(
        DbContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Seed(context);

        return Task.CompletedTask;
    }

}
