using PurePad.Search;
using PurePad.Tests.Fakes;

namespace PurePad.Tests;

public sealed class SearchServiceTests
{
    [Fact]
    public void FindNext_down_selects_next_match_after_caret()
    {
        var editor = new InMemoryTextEditor("foo bar foo");
        var service = new SearchService(editor);

        bool found = service.FindNext(new SearchRequest("foo", matchCase: true, searchDown: true));

        Assert.True(found);
        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal("foo", editor.SelectedText);
    }

    [Fact]
    public void FindNext_advances_past_current_selection()
    {
        var editor = new InMemoryTextEditor("foo bar foo");
        var service = new SearchService(editor);
        var request = new SearchRequest("foo", matchCase: true, searchDown: true);

        service.FindNext(request); // selects index 0
        bool found = service.FindNext(request);

        Assert.True(found);
        Assert.Equal(8, editor.SelectionStart);
    }

    [Fact]
    public void FindNext_up_searches_backward()
    {
        var editor = new InMemoryTextEditor("foo bar foo");
        var service = new SearchService(editor);
        editor.Select(11, 0); // caret at end

        bool found = service.FindNext(new SearchRequest("foo", matchCase: true, searchDown: false));

        Assert.True(found);
        Assert.Equal(8, editor.SelectionStart);
    }

    [Fact]
    public void FindNext_is_case_insensitive_when_requested()
    {
        var editor = new InMemoryTextEditor("Hello WORLD");
        var service = new SearchService(editor);

        bool found = service.FindNext(new SearchRequest("world", matchCase: false, searchDown: true));

        Assert.True(found);
        Assert.Equal(6, editor.SelectionStart);
    }

    [Fact]
    public void FindNext_returns_false_when_absent()
    {
        var editor = new InMemoryTextEditor("abc");
        var service = new SearchService(editor);

        Assert.False(service.FindNext(new SearchRequest("z", matchCase: true, searchDown: true)));
    }

    [Fact]
    public void Replace_replaces_current_match_then_finds_next()
    {
        var editor = new InMemoryTextEditor("foo foo");
        var service = new SearchService(editor);
        var request = new SearchRequest("foo", matchCase: true, searchDown: true, replacement: "bar");

        service.FindNext(request);   // select first "foo"
        service.Replace(request);    // replace it, move to second
        service.Replace(request);    // replace second

        Assert.Equal("bar bar", editor.Text);
    }

    [Fact]
    public void ReplaceAll_replaces_every_occurrence()
    {
        var editor = new InMemoryTextEditor("a.a.a");
        var service = new SearchService(editor);

        int count = service.ReplaceAll(new SearchRequest("a", matchCase: true, searchDown: true, replacement: "b"));

        Assert.Equal(3, count);
        Assert.Equal("b.b.b", editor.Text);
    }

    [Fact]
    public void Empty_query_is_a_no_op()
    {
        var editor = new InMemoryTextEditor("text");
        var service = new SearchService(editor);

        Assert.False(service.FindNext(new SearchRequest("", matchCase: true, searchDown: true)));
        Assert.Equal(0, service.ReplaceAll(new SearchRequest("", matchCase: true, searchDown: true, replacement: "x")));
    }
}
