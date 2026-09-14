using PurePad.Formatting;

namespace PurePad.Tests;

/// <summary>Test helpers for inspecting the tokens a highlighter produces.</summary>
internal static class TokenExtensions
{
    /// <summary>The source substrings of every token of a given kind, in order.</summary>
    public static string[] TextsOf(this IEnumerable<TextToken> tokens, string source, TokenKind kind) =>
        tokens
            .Where(t => t.Kind == kind)
            .Select(t => source.Substring(t.Start, t.Length))
            .ToArray();

    /// <summary>Assert every token stays within the bounds of <paramref name="source"/>.</summary>
    public static void AssertWithinBounds(this IEnumerable<TextToken> tokens, string source)
    {
        foreach (TextToken token in tokens)
        {
            Assert.True(token.Start >= 0, "token start is negative");
            Assert.True(token.End <= source.Length, "token extends past the source");
        }
    }
}
