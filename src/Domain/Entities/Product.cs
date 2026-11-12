using System;

namespace CleanArchitecture.Domain.Entities;




public class Product : BaseAuditableEntity
{
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; } = 0;
    public int ReorderLevel { get; set; } = 5;
    public bool IsActive { get; set; }

    public bool TryDeductStock(int quantity)
    {
        if (StockQuantity >= quantity)
        {
            StockQuantity -= quantity;
            return true;
        }
        return false;
    }
}
