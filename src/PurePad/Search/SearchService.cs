using PurePad.Editor;

namespace PurePad.Search;

/// <summary>
/// Encapsulates find/replace against an <see cref="ITextEditor"/>. Keeping this logic out
/// of the dialogs makes it reusable (the Find dialog, Replace dialog and the F3 shortcut
/// all share it) and independently testable (Single Responsibility).
/// </summary>
public sealed class SearchService
{
    private readonly ITextEditor _editor;

    public SearchService(ITextEditor editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    /// <summary>
    /// Find the next match relative to the current selection and select it.
    /// Returns true when a match was found.
    /// </summary>
    public bool FindNext(SearchRequest request)
    {
        if (request.IsEmpty)
        {
            return false;
        }

        string text = _editor.Text;
        int index = request.SearchDown
            ? FindForward(text, request)
            : FindBackward(text, request);

        if (index < 0)
        {
            return false;
        }

        _editor.Select(index, request.Query.Length);
        _editor.ScrollToCaret();
        return true;
    }

    /// <summary>
    /// Replace the current selection if it matches the query, then advance to the next
    /// match. Returns true when a replacement was made.
    /// </summary>
    public bool Replace(SearchRequest request)
    {
        if (request.IsEmpty)
        {
            return false;
        }

        if (_editor.SelectionLength == request.Query.Length &&
            string.Equals(_editor.SelectedText, request.Query, request.Comparison))
        {
            _editor.InsertText(request.Replacement);
        }

        return FindNext(request);
    }

    /// <summary>Replace every match in the document. Returns the number of replacements.</summary>
    public int ReplaceAll(SearchRequest request)
    {
        if (request.IsEmpty)
        {
            return 0;
        }

        string text = _editor.Text;
        var builder = new System.Text.StringBuilder(text.Length);
        int count = 0;
        int cursor = 0;

        while (true)
        {
            int match = text.IndexOf(request.Query, cursor, request.Comparison);
            if (match < 0)
            {
                builder.Append(text, cursor, text.Length - cursor);
                break;
            }

            builder.Append(text, cursor, match - cursor);
            builder.Append(request.Replacement);
            cursor = match + request.Query.Length;
            count++;
        }

        if (count > 0)
        {
            _editor.Text = builder.ToString();
            _editor.Modified = true;
        }

        return count;
    }

    private int FindForward(string text, SearchRequest request)
    {
        int start = _editor.SelectionStart + _editor.SelectionLength;
        if (start > text.Length)
        {
            start = text.Length;
        }

        return text.IndexOf(request.Query, start, request.Comparison);
    }

    private int FindBackward(string text, SearchRequest request)
    {
        int caret = _editor.SelectionStart;
        int from = caret - 1;
        if (from < 0)
        {
            return -1;
        }

        if (from > text.Length - 1)
        {
            from = text.Length - 1;
        }

        return text.LastIndexOf(request.Query, from, request.Comparison);
    }
}
