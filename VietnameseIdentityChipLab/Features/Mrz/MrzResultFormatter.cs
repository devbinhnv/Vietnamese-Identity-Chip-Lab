using Icao9303.Core;

namespace VietnameseIdentityChipLab;

/// <summary>Only document fields for the user; no OCR diagnostics or access-key material.</summary>
public static class MrzResultFormatter
{
    public static string Format(Td1Mrz mrz)
    {
        var name = string.Join(' ', mrz.Third.Split('<', StringSplitOptions.RemoveEmptyEntries));
        var sex = mrz.Second[7] switch { 'M' => "Nam", 'F' => "Nữ", _ => "Không ghi" };
        return $"Họ tên: {name}\n" +
            $"Số giấy tờ: {mrz.DocumentNumber}\n" +
            $"Ngày sinh: {Date(mrz.BirthDate)}\n" +
            $"Giới tính: {sex}\n" +
            $"Quốc tịch: {mrz.Second.Substring(15, 3).Replace('<', ' ').Trim()}\n" +
            $"Ngày hết hạn: {Date(mrz.ExpiryDate)}\n" +
            $"Loại giấy tờ: {mrz.First[..2].TrimEnd('<')}\n" +
            $"Nơi cấp: {mrz.First.Substring(2, 3).Replace('<', ' ').Trim()}";
    }

    // TD1 encodes only a two-digit year. Preserve that precision instead of inventing a century.
    private static string Date(string value) => $"{value[4..6]}/{value[2..4]}/{value[..2]}";
}
