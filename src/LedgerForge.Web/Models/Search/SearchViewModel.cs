namespace LedgerForge.Web.Models.Search;

public sealed record SearchResultViewModel(
    string Type,
    string Reference,
    string Title,
    string? Detail,
    string Url);

public sealed record SearchViewModel(
    string Query,
    IReadOnlyList<SearchResultViewModel> Results);
