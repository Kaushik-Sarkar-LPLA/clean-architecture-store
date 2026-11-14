using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Application.Products.Commands.DeductProductStock;


using MediatR;

public record DeductProductStockCommand : IRequest<DeductStockResult>
{
    public int ProductId { get; init; }
    public int QuantityToDeduct { get; init; }
}
