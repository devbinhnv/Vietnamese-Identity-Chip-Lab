using Icao9303.Core;

namespace VietnameseIdentityChipLab;

/// <summary>Only validated data read from the chip may be presented as a successful read.</summary>
public sealed record ChipMrzResult(Td1Mrz Mrz, bool MatchesConfirmed)
{
    public static ChipMrzResult FromDg1(IEnumerable<string> chipLines, Td1Mrz confirmed)
    {
        var lines = chipLines.ToArray();
        if (lines.Length != 3) throw new FormatException("DG1 không có đúng ba dòng MRZ TD1.");
        var parsed = Td1Mrz.ParseValidated(lines[0], lines[1], lines[2]);
        return new(parsed, lines.SequenceEqual([confirmed.First, confirmed.Second, confirmed.Third]));
    }

    public string CompletionMessage(bool photoAvailable) => !MatchesConfirmed
        ? "Đã đọc chip nhưng thông tin khác MRZ. Kiểm tra lại giấy tờ."
        : photoAvailable
            ? "Đọc CCCD thành công. Thông tin trong chip khớp với MRZ."
            : "Đã đọc thông tin CCCD và khớp MRZ. Chưa hiển thị được ảnh chân dung.";
}
