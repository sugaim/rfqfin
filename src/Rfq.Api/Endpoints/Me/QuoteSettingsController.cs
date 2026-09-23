using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Api.RfqQuotes;
using Rfq.Application;

namespace Rfq.Api.Endpoints.CurrentUser;

[ApiController]
[Route("api/me/settings")]
public sealed class QuoteSettingsController(
    ICurrentUser currentUser,
    IQuoteExpirySettings expirySettings,
    IQuoteModeSettings quoteModeSettings) : ControllerBase
{
    [HttpGet("quote-expiry")]
    [EndpointName("GetQuoteExpiry")]
    public async Task<QuoteExpiryResponse> GetQuoteExpiry(CancellationToken cancellationToken) =>
        QuoteApiMapper.ToApi(await expirySettings.GetAsync(
            currentUser.User.UserId, cancellationToken));

    [HttpPut("quote-expiry")]
    [EndpointName("SaveQuoteExpiry")]
    public async Task<QuoteExpiryResponse> SaveQuoteExpiry(
        QuoteExpiryRequest request, CancellationToken cancellationToken) =>
        QuoteApiMapper.ToApi(await expirySettings.SaveAsync(
            currentUser.User.UserId,
            QuoteApiMapper.ToDomain(request),
            cancellationToken));

    [HttpGet("default-quote-mode")]
    [EndpointName("GetDefaultQuoteMode")]
    public async Task<QuoteModeResponse> GetDefaultQuoteMode(
        CancellationToken cancellationToken) => new(QuoteApiMapper.ToApi(
            await quoteModeSettings.GetAsync(currentUser.User.UserId, cancellationToken)));

    [HttpPut("default-quote-mode")]
    [EndpointName("SaveDefaultQuoteMode")]
    public async Task<QuoteModeResponse> SaveDefaultQuoteMode(
        QuoteModeRequest request, CancellationToken cancellationToken) => new(QuoteApiMapper.ToApi(
            await quoteModeSettings.SaveAsync(
                currentUser.User.UserId,
                QuoteApiMapper.ToDomain(request.Mode),
                cancellationToken)));
}

public sealed record QuoteModeRequest([Required] QuoteMode Mode);
public sealed record QuoteModeResponse(QuoteMode Mode);
