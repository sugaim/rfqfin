using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.Endpoints.CurrentUser;

[ApiController]
[Route("api/me/grid-configs/{screenId}/{configKey}")]
public sealed class GridConfigsController(IGridConfigStore gridConfigs) : ControllerBase
{
    [HttpGet]
    [EndpointName("GetGridConfig")]
    public async Task<ActionResult<GridConfigResponse>> Get(
        string screenId, string configKey, CancellationToken cancellationToken)
    {
        GridConfig? value = await gridConfigs.GetAsync(screenId, configKey, cancellationToken);
        return value is null ? NotFound() : Ok(ToApi(value));
    }

    [HttpPut]
    [EndpointName("SaveGridConfig")]
    public async Task<GridConfigResponse> Save(
        string screenId,
        string configKey,
        GridConfigRequest request,
        CancellationToken cancellationToken) => ToApi(await gridConfigs.SaveAsync(
            screenId, configKey, request.Version, request.Config.GetRawText(), cancellationToken));

    private static GridConfigResponse ToApi(GridConfig value)
    {
        using var document = JsonDocument.Parse(value.Config);
        return new(
            value.ScreenId,
            value.ConfigKey,
            value.Version,
            document.RootElement.Clone(),
            value.UpdatedAt);
    }
}

public sealed record GridConfigRequest(
    [Range(1, int.MaxValue)] int Version,
    JsonElement Config);

public sealed record GridConfigResponse(
    string ScreenId,
    string ConfigKey,
    int Version,
    JsonElement Config,
    DateTimeOffset UpdatedAt);
