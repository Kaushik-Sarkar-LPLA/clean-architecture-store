
using CleanArchitecture.Application.Common.Interfaces;
using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Application.Products.Commands.DeductProductStock;


public record DeductStockResult(bool IsSuccess, int RemainingStock, string? ErrorMessage = null);
