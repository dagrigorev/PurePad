using PurePad.LargeFiles;

namespace PurePad.Tests;

public sealed class PieceTableTests
{
    private static PieceTable Create(string text) => new(new StringOriginalText(text));

    [Fact]
    public void New_table_mirrors_the_original()
    {
        var table = Create("hello world");

        Assert.Equal(11, table.Length);
        Assert.Equal("hello world", table.GetAllText());
    }

    [Fact]
    public void Insert_at_start_middle_and_end()
    {
        var table = Create("BC");
        table.Insert(0, "A");     // ABC
        table.Insert(3, "E");     // ABCE
        table.Insert(3, "D");     // ABCDE

        Assert.Equal("ABCDE", table.GetAllText());
        Assert.Equal(5, table.Length);
    }

    [Fact]
    public void Delete_range_spanning_pieces()
    {
        var table = Create("Hello");
        table.Insert(5, ", world"); // "Hello, world"
        table.Delete(0, 7);         // remove "Hello, " -> "world"

        Assert.Equal("world", table.GetAllText());
        Assert.Equal(5, table.Length);
    }

    [Fact]
    public void GetText_reads_across_original_and_add_pieces()
    {
        var table = Create("aaaZZbbb");
        table.Delete(3, 2);          // remove "ZZ" -> "aaabbb"
        table.Insert(3, "-XYZ-");    // "aaa-XYZ-bbb"

        Assert.Equal("aaa-XYZ-bbb", table.GetAllText());
        Assert.Equal("-XYZ-", table.GetText(3, 5));
        Assert.Equal("aaa", table.GetText(0, 3));
        Assert.Equal("bbb", table.GetText(8, 3));
    }

    [Fact]
    public void Line_index_tracks_original_newlines()
    {
        var table = Create("one\ntwo\nthree");

        Assert.Equal(3, table.LineCount);
        Assert.Equal("one", table.GetLine(0));
        Assert.Equal("two", table.GetLine(1));
        Assert.Equal("three", table.GetLine(2));
    }

    [Fact]
    public void Inserting_a_newline_splits_a_line()
    {
        var table = Create("abcdef");
        table.Insert(3, "\n");

        Assert.Equal(2, table.LineCount);
        Assert.Equal("abc", table.GetLine(0));
        Assert.Equal("def", table.GetLine(1));
    }

    [Fact]
    public void Deleting_a_newline_merges_lines()
    {
        var table = Create("abc\ndef");
        Assert.Equal(2, table.LineCount);

        table.Delete(3, 1); // remove the '\n'

        Assert.Equal(1, table.LineCount);
        Assert.Equal("abcdef", table.GetLine(0));
    }

    [Fact]
    public void Typing_characters_into_a_middle_line_keeps_lines_correct()
    {
        var table = Create("a\nb\nc");
        table.Insert(2, "XYZ"); // into line 1 ("b" -> "XYZb")

        Assert.Equal(3, table.LineCount);
        Assert.Equal("a", table.GetLine(0));
        Assert.Equal("XYZb", table.GetLine(1));
        Assert.Equal("c", table.GetLine(2));
    }

    [Fact]
    public void GetLine_strips_crlf()
    {
        var table = Create("x\r\ny");

        Assert.Equal(2, table.LineCount);
        Assert.Equal("x", table.GetLine(0));
        Assert.Equal("y", table.GetLine(1));
    }

    [Fact]
    public void LineFromPosition_locates_the_line()
    {
        var table = Create("aa\nbbb\nc"); // starts: 0, 3, 7

        Assert.Equal(0, table.LineFromPosition(0));
        Assert.Equal(0, table.LineFromPosition(2));
        Assert.Equal(1, table.LineFromPosition(3));
        Assert.Equal(1, table.LineFromPosition(6));
        Assert.Equal(2, table.LineFromPosition(7));
    }

    [Fact]
    public void Uses_precomputed_original_line_starts()
    {
        // Simulate the large-file path: line starts supplied instead of scanned.
        var table = new PieceTable(new StringOriginalText("aa\nbb\ncc"), new[] { 3, 6 });

        Assert.Equal(3, table.LineCount);
        Assert.Equal("aa", table.GetLine(0));
        Assert.Equal("bb", table.GetLine(1));
        Assert.Equal("cc", table.GetLine(2));
    }

    [Fact]
    public void Sequence_of_edits_matches_a_plain_string()
    {
        var table = Create("The quick brown fox");
        var reference = new System.Text.StringBuilder("The quick brown fox");

        void Ins(int p, string s) { table.Insert(p, s); reference.Insert(p, s); }
        void Del(int p, int c) { table.Delete(p, c); reference.Remove(p, c); }

        Ins(19, " jumps");
        Ins(0, ">> ");
        Del(3, 4);          // remove "The "
        Ins(table.Length, "\nsecond line");
        Del(0, 3);          // remove ">> "

        Assert.Equal(reference.ToString(), table.GetAllText());
        Assert.Equal(reference.ToString().Split('\n').Length, table.LineCount);
    }
}
