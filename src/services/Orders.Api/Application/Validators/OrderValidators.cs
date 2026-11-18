using FluentValidation;
using Orders.Api.Application.DTOs;

namespace Orders.Api.Application.Validators;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Order must have at least one item.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0).LessThanOrEqualTo(1000);
        });

        RuleFor(x => x.ShippingMethod)
            .IsInEnum().WithMessage("Invalid shipping method.");
    }
}
