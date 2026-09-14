namespace PurePad.Search;

/// <summary>Immutable parameters for a find/replace operation.</summary>
public sealed class SearchRequest
{
    public SearchRequest(string query, bool matchCase, bool searchDown, string? replacement = null)
    {
        Query = query ?? string.Empty;
        MatchCase = matchCase;
        SearchDown = searchDown;
        Replacement = replacement ?? string.Empty;
    }

    /// <summary>The text to look for.</summary>
    public string Query { get; }

    /// <summary>Whether the search is case-sensitive.</summary>
    public bool MatchCase { get; }

    /// <summary>True to search towards the end of the document, false towards the start.</summary>
    public bool SearchDown { get; }

    /// <summary>The replacement text (used by replace operations only).</summary>
    public string Replacement { get; }

    public bool IsEmpty => Query.Length == 0;

    public StringComparison Comparison =>
        MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
}
