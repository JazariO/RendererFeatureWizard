using System;

public static partial class RendererFeatureGenerator
{
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

        var normalizedReplacement = "\n" + (replacementBlockText ?? "").Trim('\r', '\n') + "\n";
        normalizedReplacement = normalizedReplacement.Replace("\r\n", "\n").Replace("\r", "\n");

        return before + normalizedReplacement + after;
    }
}

