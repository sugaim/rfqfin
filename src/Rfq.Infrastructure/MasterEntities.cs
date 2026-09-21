namespace Rfq.Infrastructure;

internal sealed class DeskEntity
{
    public string DeskId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = string.Empty;
}

internal sealed class MasterUserEntity
{
    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string DeskId { get; set; } = string.Empty;

    public string[] Roles { get; set; } = [];
}

internal sealed class CategoryEntity
{
    public string CategoryId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

internal sealed class CategoryRoutingEntity
{
    public string CategoryId { get; set; } = string.Empty;

    public string DefaultTraderId { get; set; } = string.Empty;
}

internal sealed class ClientEntity
{
    public string ClientId { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

internal sealed class SecurityEntity
{
    public string SecurityId { get; set; } = string.Empty;

    public string JapaneseName { get; set; } = string.Empty;

    public string BbgDisplay { get; set; } = string.Empty;

    public string BbgSearchText { get; set; } = string.Empty;

    public string InternalCode { get; set; } = string.Empty;

    public string Isin { get; set; } = string.Empty;

    public string CategoryId { get; set; } = string.Empty;

    public CategoryEntity Category { get; set; } = null!;
}

internal sealed class SystemDateEntity
{
    public string Key { get; set; } = string.Empty;

    public DateOnly BusinessDate { get; set; }
}
