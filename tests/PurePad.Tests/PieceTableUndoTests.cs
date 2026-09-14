using PurePad.LargeFiles;

namespace PurePad.Tests;

public sealed class PieceTableUndoTests
{
    private static PieceTable Create(string text) => new(new StringOriginalText(text));

    [Fact]
    public void Undo_reverses_an_insert()
    {
        var table = Create("hello");
        table.Insert(5, " world");

        Assert.True(table.CanUndo);
        int caret = table.Undo();

        Assert.Equal("hello", table.GetAllText());
        Assert.Equal(5, caret);
        Assert.False(table.CanUndo);
    }

    [Fact]
    public void Undo_reverses_a_delete()
    {
        var table = Create("hello world");
        table.Delete(5, 6); // remove " world"

        int caret = table.Undo();

        Assert.Equal("hello world", table.GetAllText());
        Assert.Equal(11, caret); // caret after the re-inserted text
    }

    [Fact]
    public void Redo_reapplies_an_undone_edit()
    {
        var table = Create("abc");
        table.Insert(3, "def");
        table.Undo();

        Assert.True(table.CanRedo);
        int caret = table.Redo();

        Assert.Equal("abcdef", table.GetAllText());
        Assert.Equal(6, caret);
    }

    [Fact]
    public void A_new_edit_clears_the_redo_stack()
    {
        var table = Create("abc");
        table.Insert(3, "X");
        table.Undo();
        Assert.True(table.CanRedo);

        table.Insert(3, "Y");

        Assert.False(table.CanRedo);
        Assert.Equal("abcY", table.GetAllText());
    }

    [Fact]
    public void Consecutive_typing_coalesces_into_one_undo()
    {
        var table = Create("");
        table.Insert(0, "h");
        table.Insert(1, "i");
        table.Insert(2, "!");

        Assert.Equal("hi!", table.GetAllText());
        table.Undo(); // one step should remove all three typed chars

        Assert.Equal(string.Empty, table.GetAllText());
        Assert.False(table.CanUndo);
    }

    [Fact]
    public void Backspacing_coalesces_into_one_undo()
    {
        var table = Create("abcdef");
        // Backspace three times from position 6.
        table.Delete(5, 1);
        table.Delete(4, 1);
        table.Delete(3, 1);
        Assert.Equal("abc", table.GetAllText());

        table.Undo();

        Assert.Equal("abcdef", table.GetAllText());
    }

    [Fact]
    public void Undo_and_redo_keep_the_line_index_correct()
    {
        var table = Create("one\ntwo");
        table.Insert(3, "\nmiddle"); // one\nmiddle\ntwo
        Assert.Equal(3, table.LineCount);

        table.Undo();
        Assert.Equal(2, table.LineCount);
        Assert.Equal("one", table.GetLine(0));
        Assert.Equal("two", table.GetLine(1));

        table.Redo();
        Assert.Equal(3, table.LineCount);
        Assert.Equal("middle", table.GetLine(1));
    }

    [Fact]
    public void Multiple_undo_then_redo_round_trips()
    {
        var table = Create("start");
        table.Insert(5, " A");
        table.Insert(7, " B");
        table.Delete(0, 5); // remove "start"

        table.Undo();
        table.Undo();
        table.Undo();
        Assert.Equal("start", table.GetAllText());

        table.Redo();
        table.Redo();
        table.Redo();
        Assert.Equal(" A B", table.GetAllText());
    }
}
