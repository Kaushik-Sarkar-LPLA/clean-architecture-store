using CleanArchitecture.Application.Products.Commands.DeductProductStock;
using Microsoft.AspNetCore.Http.HttpResults;
using CleanArchitecture.Application.Reviews.Commands.CreateReview;


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

    public static async Task<IResult> CreateProductReview(
    ISender sender,
    int productId,
    CreateReviewCommand command)
{
    if (productId != command.ProductId)
    {
        return Results.BadRequest("Route productId does not match command ProductId.");
    }

    var reviewId = await sender.Send(command);

    return Results.Created($"/api/Products/{productId}/reviews/{reviewId}", reviewId);
}

}
