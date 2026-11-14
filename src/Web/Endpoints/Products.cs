using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Application.Products.Commands.DeductProductStock;

public static class ProductsEndpoints
{
    public static RouteGroupBuilder MapProductsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/Products");

        group.MapPost("/{id}/deduct-stock", DeductStock)
             .WithName("DeductProductStock")
             
            .WithSummary("Deduct product stock quantity");


        return group;
    }

    public static async Task<Results<Ok<DeductStockResult>, BadRequest<DeductStockResult>, NotFound>> DeductStock(
        ISender sender, int id, DeductProductStockCommand command)
    {
        if (id != command.ProductId)
        {
            return TypedResults.BadRequest(new DeductStockResult(false, 0, "Route ID and Command ID mismatch"));
        }

        var result = await sender.Send(command);

        if (!result.IsSuccess)
        {
            return TypedResults.BadRequest(result);
        }

        return TypedResults.Ok(result);
    }
}
