using Icao9303.Core;

namespace VietnameseIdentityChipLab;

/// <summary>UI boundary: split edited text into exact lines before the ICAO parser sees it.</summary>
public static class MrzAccess
{
    public static bool TryPrepare(string editedText, out Td1Mrz? mrz, out string error)
    {
        mrz = null;
        var normalized = editedText.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        if (normalized.EndsWith('\n')) normalized = normalized[..^1];
        var lines = normalized.Split('\n');
        if (lines.Length != 3)
        {
            error = "Cần đúng 3 dòng TD1. Hãy sửa bản OCR và đối chiếu giấy tờ.";
            return false;
        }

        try
        {
            mrz = Td1Mrz.ParseValidated(lines[0], lines[1], lines[2]);
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }
}
