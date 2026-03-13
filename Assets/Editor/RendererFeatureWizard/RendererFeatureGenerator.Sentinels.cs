using System;

public static partial class RendererFeatureGenerator
{
    private static string DetectPreferredNewline(string text)
    {
        // Favor CRLF if present; Unity on Windows commonly expects it, and it avoids mixed endings on injection.
        return text != null && text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    }

    private static string NormalizeLineEndings(string text, string newline)
    {
        if (text == null)
            return "";

        if (string.IsNullOrEmpty(newline))
            newline = "\n";

        // Collapse to '\n' first, then expand.
        var t = text.Replace("\r\n", "\n").Replace("\r", "\n");
        if (newline == "\n")
            return t;

        return t.Replace("\n", newline);
    }

    private static bool TryGetSentinelBlock(string text, string tag, out string blockText)
    {
        blockText = null;
        var start = $"// <{tag}>";
        var end = $"// </{tag}>";

        var startIdx = text.IndexOf(start, StringComparison.Ordinal);
        if (startIdx < 0)
            return false;

        var endIdx = text.IndexOf(end, startIdx, StringComparison.Ordinal);
        if (endIdx < 0)
            return false;

        var startContentIdx = startIdx + start.Length;
        var content = text.Substring(startContentIdx, endIdx - startContentIdx);
        blockText = content.Trim('\r', '\n');
        return true;
    }

    private static string ReplaceSentinelBlock(string text, string tag, string replacementBlockText)
    {
        var start = $"// <{tag}>";
        var end = $"// </{tag}>";

        var startIdx = text.IndexOf(start, StringComparison.Ordinal);
        if (startIdx < 0)
            return text;

        var endIdx = text.IndexOf(end, startIdx, StringComparison.Ordinal);
        if (endIdx < 0)
            return text;

        var startContentIdx = startIdx + start.Length;
        var endContentIdx = endIdx;

        var before = text.Substring(0, startContentIdx);
        var after = text.Substring(endContentIdx);

        var nl = DetectPreferredNewline(text);
        var replacement = (replacementBlockText ?? "").Trim('\r', '\n');
        var normalizedReplacement = nl + NormalizeLineEndings(replacement, nl) + nl;

        return before + normalizedReplacement + after;
    }
}
