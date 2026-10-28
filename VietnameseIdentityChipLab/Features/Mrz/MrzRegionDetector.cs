namespace VietnameseIdentityChipLab;

/// <summary>OCR geometry in the upright image; this is a heuristic, not an ICAO rule.</summary>
public readonly record struct MrzOcrLine(string Text, int Left, int Top, int Right, int Bottom,
    float AngleDegrees = 0);

public readonly record struct MrzImageRegion(int Left, int Top, int Width, int Height,
    float AngleDegrees);

public static class MrzRegionDetector
{
    /// <summary>
    /// Finds two or three aligned MRZ-like rows, wherever the card is in the frame.
    /// Padding protects edge characters and a missing third row. No region means use the full frame.
    /// </summary>
    public static bool TryFind(IEnumerable<MrzOcrLine> observations, int width, int height,
        out MrzImageRegion region)
    {
        region = default;
        if (width <= 0 || height <= 0) return false;
        var lines = observations.Where(IsLikelyRow).OrderBy(l => l.Top).ToArray();
        MrzOcrLine[] best = [];
        for (var i = 0; i < lines.Length; i++)
        {
            var group = new List<MrzOcrLine> { lines[i] };
            for (var j = i + 1; j < lines.Length && group.Count < 3; j++)
            {
                var previous = group[^1];
                var next = lines[j];
                var rowHeight = Math.Max(previous.Bottom - previous.Top, next.Bottom - next.Top);
                var rowWidth = Math.Max(previous.Right - previous.Left, next.Right - next.Left);
                if (next.Top >= previous.Bottom - rowHeight / 4 &&
                    next.Top - previous.Bottom <= rowHeight * 3 &&
                    Math.Abs(next.Left - previous.Left) <= rowWidth * 0.25 &&
                    Math.Min(previous.Right - previous.Left, next.Right - next.Left) >= rowWidth * 0.55)
                    group.Add(next);
            }
            if (group.Count > best.Length) best = group.ToArray();
        }
        if (best.Length < 2) return false;

        var left = best.Min(l => l.Left);
        var right = best.Max(l => l.Right);
        var top = best.Min(l => l.Top);
        var bottom = best.Max(l => l.Bottom);
        var paddingX = Math.Max(8, (int)((right - left) * 0.08));
        var paddingY = (int)Math.Ceiling(best.Max(l => l.Bottom - l.Top) *
            (best.Length == 2 ? 2.5 : 0.75));
        left = Math.Clamp(left - paddingX, 0, width);
        right = Math.Clamp(right + paddingX, 0, width);
        top = Math.Clamp(top - paddingY, 0, height);
        bottom = Math.Clamp(bottom + paddingY, 0, height);
        if (right <= left || bottom <= top) return false;
        var angle = best.Select(l => l.AngleDegrees).Order().ElementAt(best.Length / 2);
        region = new(left, top, right - left, bottom - top,
            float.IsFinite(angle) && Math.Abs(angle) <= 8 ? angle : 0);
        return true;
    }

    private static bool IsLikelyRow(MrzOcrLine line)
    {
        var compact = string.Concat(line.Text.Where(c => c is not (' ' or '\t')));
        return line.Right > line.Left && line.Bottom > line.Top &&
            compact.Length is >= 18 and <= 44 &&
            compact.Count(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '<') >= compact.Length * 0.85;
    }
}
