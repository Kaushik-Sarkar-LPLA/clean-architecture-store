using CleanArchitecture.Application.Products.Commands.DeductProductStock;
using CleanArchitecture.Application.Reviews.Commands.CreateReview;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CleanArchitecture.Web.Endpoints;

public class Products : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapPost(DeductStock, "{id}/deduct-stock");

        groupBuilder.MapPost("/{productId:int}/reviews", CreateProductReview)
            .WithName("CreateProductReview");
    }

    [EndpointSummary("Deduct product stock")]
    [EndpointDescription("Validates active product availability and atomically decrements StockQuantity during checkout.")]
    public static async Task<Results<Ok<DeductStockResult>, BadRequest<DeductStockResult>, NotFound>> DeductStock(
        ISender sender,
        int id,
        DeductProductStockCommand command)
    {
        if (id != command.ProductId)
        {
            return TypedResults.BadRequest(
                new DeductStockResult(false, 0, "Route ID does not match ProductId in request body."));
        }

        var result = await sender.Send(command);

        if (!result.IsSuccess)
        {
            return TypedResults.BadRequest(result);
        }

        return TypedResults.Ok(result);
    }

    [EndpointSummary("Create product review")]
    [EndpointDescription("Creates a verified review for an existing product and recalculates its average rating and total review count.")]
    public static async Task<Results<Created<int>, BadRequest<string>, NotFound>> CreateProductReview(
        ISender sender,
        int productId,
        CreateReviewCommand command)
    {
        if (productId != command.ProductId)
        {
            return TypedResults.BadRequest("Route productId does not match command ProductId.");
        }

        var reviewId = await sender.Send(command);

        return TypedResults.Created($"/api/Products/{productId}/reviews/{reviewId}", reviewId);
    }
}

