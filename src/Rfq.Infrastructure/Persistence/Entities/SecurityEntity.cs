namespace Rfq.Infrastructure;

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

