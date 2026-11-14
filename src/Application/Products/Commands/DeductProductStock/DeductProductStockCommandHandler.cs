using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Application.Products.Commands.DeductProductStock;


using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Domain.Exceptions;
using MediatR;

public class DeductProductStockCommandHandler : IRequestHandler<DeductProductStockCommand, DeductStockResult>
{
    private readonly IApplicationDbContext _context;

    public DeductProductStockCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DeductStockResult> Handle(DeductProductStockCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products.FindAsync([request.ProductId], cancellationToken);

        if (product == null)
        {
            throw new NotFoundException(nameof(Product), request.ProductId.ToString());
        }

        if (!product.IsActive || !product.TryDeductStock(request.QuantityToDeduct))
        {
            return new DeductStockResult(false, product.StockQuantity, "Insufficient stock available");
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new DeductStockResult(true, product.StockQuantity);
    }
}
