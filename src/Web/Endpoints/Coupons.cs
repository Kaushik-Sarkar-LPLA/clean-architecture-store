using CleanArchitecture.Application.Coupons.Queries.ValidateCoupon;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CleanArchitecture.Web.Endpoints;

public class Coupons : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapPost(ValidateCoupon, "validate");
    }

    [EndpointSummary("Validate promotional coupon")]
    [EndpointDescription("Validates a promotional coupon code against active status, expiration, minimum subtotal, and usage limits, returning calculated discount and final total.")]
    public static async Task<Ok<CouponValidationResult>> ValidateCoupon(
        ISender sender,
        ValidateCouponQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }
}
