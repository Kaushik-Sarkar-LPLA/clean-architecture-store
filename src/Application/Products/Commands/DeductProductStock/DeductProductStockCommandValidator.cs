using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Application.Products.Commands.DeductProductStock;


using FluentValidation;

public class DeductProductStockCommandValidator : AbstractValidator<DeductProductStockCommand>
{
    public DeductProductStockCommandValidator()
    {
        RuleFor(v => v.ProductId)
            .GreaterThan(0)
            .WithMessage("ProductId must be greater than zero.");

        RuleFor(v => v.QuantityToDeduct)
            .GreaterThan(0)
            .WithMessage("QuantityToDeduct must be at least 1.");
    }
}
