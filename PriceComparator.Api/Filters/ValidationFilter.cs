using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace PriceComparator.Api.Filters;

public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>)
                .MakeGenericType(argument.GetType());

            var validators = context.HttpContext.RequestServices
                .GetServices(validatorType)
                .Cast<IValidator>();

            foreach (var validator in validators)
            {
                var validationContext =
                    new ValidationContext<object>(argument);

                var result = await validator.ValidateAsync(
                    validationContext,
                    context.HttpContext.RequestAborted);

                foreach (var error in result.Errors)
                {
                    context.ModelState.AddModelError(
                        error.PropertyName,
                        error.ErrorMessage);
                }
            }
        }

        if (!context.ModelState.IsValid)
        {
            var options = context.HttpContext.RequestServices
                .GetRequiredService<IOptions<ApiBehaviorOptions>>()
                .Value;

            context.Result =
                options.InvalidModelStateResponseFactory(context);

            return;
        }

        await next();
    }
}