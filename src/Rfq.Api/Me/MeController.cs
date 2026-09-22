using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Rfq.Api.RfqQuotes;
using Rfq.Application;

namespace Rfq.Api.Me;

[ApiController]
[Route("api/me")]
public sealed class MeController(
    ICurrentUser currentUser,
    IQuoteExpirySettings expirySettings,
    IQuoteModeSettings quoteModeSettings,
    IGridConfigStore gridConfigs) : ControllerBase
{
    [HttpGet]
    public MeResponse Get() => new(
        currentUser.User.UserId.Value,
        [.. currentUser.User.Roles.Select(MeApiMapper.ToApi).Order()],
        currentUser.User.DeskId.Value);

    [HttpGet("settings/quote-expiry")]
    public async Task<QuoteExpiryResponse> GetQuoteExpiry(CancellationToken cancellationToken) =>
        QuoteApiMapper.ToApi(await expirySettings.GetAsync(
            currentUser.User.UserId, cancellationToken));

    [HttpPut("settings/quote-expiry")]
    public async Task<QuoteExpiryResponse> PutQuoteExpiry(
        QuoteExpiryRequest request,
        CancellationToken cancellationToken) => QuoteApiMapper.ToApi(
            await expirySettings.SaveAsync(
                currentUser.User.UserId,
                QuoteApiMapper.ToDomain(request),
                cancellationToken));

    [HttpGet("settings/default-quote-mode")]
    public async Task<QuoteModeResponse> GetDefaultQuoteMode(
        CancellationToken cancellationToken) => new(QuoteApiMapper.ToApi(
            await quoteModeSettings.GetAsync(currentUser.User.UserId, cancellationToken)));

    [HttpPut("settings/default-quote-mode")]
    public async Task<QuoteModeResponse> PutDefaultQuoteMode(
        QuoteModeRequest request,
        CancellationToken cancellationToken) => new(QuoteApiMapper.ToApi(
            await quoteModeSettings.SaveAsync(
                currentUser.User.UserId,
                QuoteApiMapper.ToDomain(request.Mode),
                cancellationToken)));

    [HttpGet("grid-configs/{screenId}/{configKey}")]
    public async Task<ActionResult<GridConfigResponse>> GetGridConfig(
        string screenId, string configKey, CancellationToken cancellationToken)
    {
        GridConfig? value = await gridConfigs.GetAsync(screenId, configKey, cancellationToken);
        return value is null ? NotFound() : Ok(MeApiMapper.ToApi(value));
    }

    [HttpPut("grid-configs/{screenId}/{configKey}")]
    public async Task<GridConfigResponse> PutGridConfig(
        string screenId,
        string configKey,
        GridConfigRequest request,
        CancellationToken cancellationToken) => MeApiMapper.ToApi(
            await gridConfigs.SaveAsync(
                screenId,
                configKey,
                request.Version,
                request.Config.GetRawText(),
                cancellationToken));
}

public enum UserRoleValue
{
    Sales,
    Trader
}

public sealed record MeResponse(string UserId, IReadOnlyList<UserRoleValue> Roles, string DeskId);
public sealed record QuoteModeRequest([Required] QuoteMode Mode);
public sealed record QuoteModeResponse(QuoteMode Mode);

public sealed record GridConfigRequest(
    [Range(1, int.MaxValue)] int Version,
    JsonElement Config);

public sealed record GridConfigResponse(
    string ScreenId,
    string ConfigKey,
    int Version,
    JsonElement Config,
    DateTimeOffset UpdatedAt);

public static class MeApiMapper
{
    public static UserRoleValue ToApi(UserRole value) => value switch
    {
        UserRole.Sales => UserRoleValue.Sales,
        UserRole.Trader => UserRoleValue.Trader,
        _ => throw new InvalidOperationException("Unknown User Role."),
    };

    public static GridConfigResponse ToApi(GridConfig value)
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
