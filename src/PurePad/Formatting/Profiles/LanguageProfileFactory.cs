using System.Drawing;
using System.Globalization;
using PurePad.Formatting.Checkers;
using PurePad.Formatting.Formatters;
using PurePad.Formatting.Highlighters;

namespace PurePad.Formatting.Profiles;

/// <summary>Builds a <see cref="LanguageDefinition"/> from a user-authored <see cref="LanguageProfile"/>.</summary>
public static class LanguageProfileFactory
{
    public static LanguageDefinition Create(LanguageProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var highlighter = new CodeSyntaxHighlighter(new CodeSyntaxOptions
        {
            Keywords = profile.Syntax.Keywords,
            LineComments = profile.Syntax.LineComments.Count > 0 ? profile.Syntax.LineComments.ToArray() : new[] { "//" },
            BlockComment = profile.Syntax.BlockComment is { } b && b.Open.Length > 0 ? (b.Open, b.Close) : null,
            RegexLiterals = profile.Syntax.RegexLiterals,
            TemplateStrings = profile.Syntax.TemplateStrings,
            CaseInsensitiveKeywords = profile.Syntax.CaseInsensitiveKeywords,
            RemLineComments = profile.Syntax.RemLineComments,
            VariableSigil = ParseSigil(profile.Syntax.VariableSigil),
        });

        var formatter = new ConfigurableFormatter(profile.Format);
        ISyntaxChecker checker = ProfileSyntaxChecker.Create(profile);
        IReadOnlyDictionary<TokenKind, Color>? colors = ParseColors(profile.Colors);
        FontSpec? font = ParseFont(profile.Font);

        // Completion vocabulary: the profile's keywords plus its explicit completion words.
        var words = new List<string>(profile.Syntax.Keywords);
        words.AddRange(profile.Completions);

        string id = string.IsNullOrWhiteSpace(profile.Id) ? "profile" : profile.Id.Trim();
        string name = string.IsNullOrWhiteSpace(profile.Name) ? id : profile.Name;
        var extensions = NormalizeExtensions(profile.Extensions);

        LspSettings? lsp = ParseLsp(profile.Lsp);

        return new LanguageDefinition(id, name, extensions, highlighter, formatter,
            checker, colors, font, words, lsp);
    }

    private static LspSettings? ParseLsp(LspProfile? lsp)
    {
        if (lsp is null || !lsp.Enabled || string.IsNullOrWhiteSpace(lsp.Command))
        {
            return null;
        }

        return new LspSettings(lsp.Command.Trim(), lsp.Args.ToArray(),
            string.IsNullOrWhiteSpace(lsp.LanguageId) ? "plaintext" : lsp.LanguageId.Trim(),
            lsp.RootMarkers.ToArray());
    }

    private static char ParseSigil(string sigil) =>
        sigil.Length > 0 && (sigil[0] == '$' || sigil[0] == '%') ? sigil[0] : '\0';

    private static IReadOnlyList<string> NormalizeExtensions(IEnumerable<string> extensions)
    {
        var list = new List<string>();
        foreach (string raw in extensions)
        {
            string e = raw.Trim();
            if (e.Length == 0)
            {
                continue;
            }

            list.Add(e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant());
        }

        return list;
    }

    private static IReadOnlyDictionary<TokenKind, Color>? ParseColors(Dictionary<string, string> colors)
    {
        if (colors.Count == 0)
        {
            return null;
        }

        var map = new Dictionary<TokenKind, Color>();
        foreach ((string key, string value) in colors)
        {
            if (Enum.TryParse(key, ignoreCase: true, out TokenKind kind) && TryParseHex(value, out Color color))
            {
                map[kind] = color;
            }
        }

        return map.Count > 0 ? map : null;
    }

    /// <summary>Parse "#RGB" or "#RRGGBB" (with or without the leading '#').</summary>
    private static bool TryParseHex(string hex, out Color color)
    {
        color = Color.Empty;
        string s = hex.Trim().TrimStart('#');
        if (s.Length == 3)
        {
            s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
        }

        if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
        {
            return false;
        }

        color = Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        return true;
    }

    private static FontSpec? ParseFont(FontProfile? font)
    {
        if (font is null || string.IsNullOrWhiteSpace(font.Family) || font.Size <= 0)
        {
            return null;
        }

        FontStyle style = FontStyle.Regular;
        foreach (string part in font.Style.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            style |= part.ToLowerInvariant() switch
            {
                "bold" => FontStyle.Bold,
                "italic" => FontStyle.Italic,
                "underline" => FontStyle.Underline,
                "strikeout" => FontStyle.Strikeout,
                _ => FontStyle.Regular,
            };
        }

        return new FontSpec(font.Family.Trim(), font.Size, style);
    }
}
