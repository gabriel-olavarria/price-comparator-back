using FluentValidation;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.UseCases.SearchProduct.Get;


namespace PriceComparator.Application.UseCases.SearchProduct.Validators;
public sealed class SearchProductValidator : AbstractValidator<ProductRequest>, ITransientDependency
{
    public SearchProductValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Debe indicar un producto para buscar.");
    }
}