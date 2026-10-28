using Icao9303.Core;

namespace VietnameseIdentityChipLab;

/// <summary>Find TD1 rows in OCR without guessing or replacing MRZ characters.</summary>
public static class LiveMrzCandidate
{
    public static bool TryFindValidatedTd1(string raw, out string candidate)
    {
        candidate = string.Empty;
        var lines = raw.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n').Split('\n')
            .Select(line => string.Concat(line.Where(c => c is not (' ' or '\t'))))
            .ToArray();
        for (var i = 0; i + 2 < lines.Length; i++)
        {
            try
            {
                Td1Mrz.ParseValidated(lines[i], lines[i + 1], lines[i + 2]);
                candidate = string.Join('\n', lines[i], lines[i + 1], lines[i + 2]);
                return true;
            }
            catch (Exception ex) when (ex is FormatException or NotSupportedException) { }
        }
        return false;
    }
}
