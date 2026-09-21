using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Api.CategoryRoutings;

[ApiController]
[Route("api/category-routings")]
public sealed class CategoryRoutingsController(ICategoryRouting routings) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<CategoryRoutingResponse>> Get(
        CancellationToken cancellationToken) => (await routings.GetAllAsync(cancellationToken))
        .Select(CategoryRoutingApiMapper.ToApi).ToArray();

    [HttpPut("{categoryId}")]
    public async Task<CategoryRoutingResponse> Put(string categoryId,
        UpdateCategoryRoutingRequest request, CancellationToken cancellationToken) =>
        CategoryRoutingApiMapper.ToApi(await routings.SetDefaultAssignedTraderAsync(
            CategoryId.Create(categoryId), UserId.Create(request.DefaultTraderId), cancellationToken));
}

public sealed record UpdateCategoryRoutingRequest(
    [Required, MinLength(1)] string DefaultTraderId);
public sealed record CategoryRoutingResponse(string CategoryId, string CategoryName,
    string DefaultTraderId, string DefaultTraderName);
public static class CategoryRoutingApiMapper
{
    public static CategoryRoutingResponse ToApi(CategoryRoutingItem value) => new(
        value.CategoryId.Value, value.CategoryName, value.DefaultTraderId.Value,
        value.DefaultTraderName);
}
