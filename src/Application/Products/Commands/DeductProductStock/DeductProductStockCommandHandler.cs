using CleanArchitecture.Application.Common.Exceptions;
using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Application.Products.Commands.DeductProductStock;

public class DeductProductStockCommandHandler : IRequestHandler<DeductProductStockCommand, DeductStockResult>
{
    private readonly IApplicationDbContext _context;

    public DeductProductStockCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DeductStockResult> Handle(DeductProductStockCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException(nameof(Product), request.ProductId.ToString());
        }

        if (!product.IsActive || !product.TryDeductStock(request.QuantityToDeduct))
        {
            return new DeductStockResult(false, product.StockQuantity, "Insufficient stock available");
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new DeductStockResult(false, product.StockQuantity, "Concurrent stock update conflict. Please retry.");
        }

        return new DeductStockResult(true, product.StockQuantity);
    }
}
