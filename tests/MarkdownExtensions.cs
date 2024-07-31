using Markdig.Renderers.Normalize;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace renovate_config.tests;

public static class MarkdownExtensions
{
    public static string ToNormalizedString(this MarkdownObject obj)
    {
        using var writer = new StringWriter();
        var renderer = new NormalizeRenderer(writer);
        renderer.Render(obj);
        return writer.ToString();
    }
    public static string InnerText(this MarkdownObject obj)
    {
        var inlines = obj.Descendants<LiteralInline>();
        return string.Join(" ", inlines.Select(inline=> inline.ToNormalizedString()));
    }
}