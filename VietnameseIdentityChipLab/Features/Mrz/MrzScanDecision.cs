namespace VietnameseIdentityChipLab;

/// <summary>Application policy only: automatic acceptance never prepares NFC access.</summary>
public static class MrzScanDecision
{
    /// <summary>A preprocessing retry must not replace useful rows with a worse OCR draft.</summary>
    public static string PreferDraft(string previous, string current)
    {
        if (LiveMrzCandidate.TryFindValidatedTd1(current, out _)) return current;
        if (LiveMrzCandidate.TryFindValidatedTd1(previous, out _)) return previous;
        return Score(current) >= Score(previous) ? current : previous;
    }

    private static int Score(string text) => text.Split('\n').Sum(line =>
    {
        var compact = string.Concat(line.Where(c => !char.IsWhiteSpace(c)));
        if (compact.Length is < 18 or > 44) return 0;
        return compact.Count(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '<') >= compact.Length * 0.85
            ? 30 - Math.Abs(30 - compact.Length) : 0;
    });

    public static bool ShouldStop(string raw, bool manual, out string draft)
    {
        if (LiveMrzCandidate.TryFindValidatedTd1(raw, out draft)) return true;
        draft = raw;
        return manual;
    }
}
