namespace VietnameseIdentityChipLab;

/// <summary>Persistent stage feedback: detecting a tag is distinct from reading its data.</summary>
public static class NfcReadFeedback
{
    public static string Checklist(int stage, bool failed = false, bool photoAvailable = false)
    {
        string[] names = ["Nhận thẻ", "Kết nối", "Xác thực truy cập", "Đọc thông tin", "Đọc ảnh chân dung"];
        return string.Join('\n', names.Select((name, index) =>
        {
            var state = index < stage ? "xong" : index == stage
                ? failed ? "lỗi" : stage == 0 ? "đang chờ" : "đang thực hiện"
                : "chưa thực hiện";
            if (stage >= names.Length && index == 4 && !photoAvailable) state = "chưa đọc được";
            return $"{name}: {state}";
        }));
    }
}
