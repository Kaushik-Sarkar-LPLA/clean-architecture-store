using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<TodoList> TodoLists { get; }

    DbSet<TodoItem> TodoItems { get; }

    DbSet<Product> Products { get; }

    DbSet<Coupon> Coupons { get; }

    DbSet<Review> Reviews { get; } 

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
