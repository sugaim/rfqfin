using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Rfq.Application;

namespace Rfq.Api.Endpoints.CurrentUser;

[ApiController]
[Route("api/me/settings/theme")]
public sealed class ThemeSettingsController(
    ICurrentUser currentUser,
    IThemeSettings themeSettings) : ControllerBase
{
    [HttpGet]
    [EndpointName("GetTheme")]
    public async Task<ThemeResponse> GetTheme(CancellationToken cancellationToken) =>
        new(ToApi(await themeSettings.GetAsync(currentUser.User.UserId, cancellationToken)));

    [HttpPut]
    [EndpointName("SaveTheme")]
    public async Task<ThemeResponse> SaveTheme(
        ThemeRequest request, CancellationToken cancellationToken) =>
        new(ToApi(await themeSettings.SaveAsync(
            currentUser.User.UserId, ToApplication(request.Mode), cancellationToken)));

    private static ThemeMode ToApi(AppThemeMode value) => value switch
    {
        AppThemeMode.Light => ThemeMode.Light,
        AppThemeMode.Dark => ThemeMode.Dark,
        _ => throw new InvalidOperationException("Unknown application theme mode."),
    };

    private static AppThemeMode ToApplication(ThemeMode? value) => value switch
    {
        ThemeMode.Light => AppThemeMode.Light,
        ThemeMode.Dark => AppThemeMode.Dark,
        null => throw new RfqRequestValidationException("Theme mode is required."),
        _ => throw new RfqRequestValidationException("Theme mode is invalid."),
    };
}

public enum ThemeMode
{
    Light,
    Dark,
}

public sealed record ThemeRequest([Required] ThemeMode? Mode);
public sealed record ThemeResponse(ThemeMode Mode);
