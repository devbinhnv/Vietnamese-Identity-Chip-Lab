using System.Text;

namespace VietnameseIdentityChipLab;

public static class NdefDecoder
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly UnicodeEncoding StrictUtf16Be = new(true, true, true);
    private static readonly UnicodeEncoding StrictUtf16Le = new(false, true, true);

    private static readonly string[] UriPrefixes =
    [
        "", "http://www.", "https://www.", "http://", "https://",
        "tel:", "mailto:", "ftp://anonymous:anonymous@", "ftp://ftp.",
        "ftps://", "sftp://", "smb://", "nfs://", "ftp://", "dav://",
        "news:", "telnet://", "imap:", "rtsp://", "urn:", "pop:",
        "sip:", "sips:", "tftp:", "btspp://", "btl2cap://", "btgoep://",
        "tcpobex://", "irdaobex://", "file://", "urn:epc:id:",
        "urn:epc:tag:", "urn:epc:pat:", "urn:epc:raw:", "urn:epc:",
        "urn:nfc:"
    ];

    public static string Hex(byte[]? bytes) =>
        bytes is null || bytes.Length == 0 ? "(trống)" : Convert.ToHexString(bytes);

    public static string Decode(short tnf, byte[]? type, byte[]? payload)
    {
        try
        {
            if (payload is null)
                throw new FormatException("Record không có payload.");
            if (type is null)
                throw new FormatException("Record không có type.");

            if (tnf == 1 && type.SequenceEqual(new byte[] { 0x54 }))
                return DecodeText(payload);

            if (tnf == 1 && type.SequenceEqual(new byte[] { 0x55 }))
                return DecodeUri(payload);

            if (tnf == 2)
            {
                var mime = Encoding.ASCII.GetString(type);
                if (mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                {
                    var charset = mime.Split(';', StringSplitOptions.TrimEntries)
                        .Skip(1)
                        .FirstOrDefault(part => part.StartsWith("charset=", StringComparison.OrdinalIgnoreCase));
                    if (charset is not null &&
                        !charset[8..].Trim('"').Equals("utf-8", StringComparison.OrdinalIgnoreCase))
                        return $"MIME {mime}: chưa hỗ trợ charset này; xem payload hex ở trên.";
                    return $"MIME {mime}: {DecodePlainText(payload)}";
                }
                return $"MIME {mime}: dữ liệu nhị phân/định dạng riêng; xem payload hex ở trên.";
            }

            if (tnf == 0)
                return "Record rỗng.";

            return "Chưa có bộ giải mã cho TNF/type này; payload hex ở trên vẫn là dữ liệu đọc được.";
        }
        catch (DecoderFallbackException ex)
        {
            return $"LỖI mã hóa ký tự: {ex.Message}. Byte gốc vẫn hiển thị ở trên.";
        }
        catch (FormatException ex)
        {
            return $"LỖI cấu trúc payload: {ex.Message}. Byte gốc vẫn hiển thị ở trên.";
        }
    }

    private static string DecodeText(byte[] payload)
    {
        if (payload.Length == 0)
            throw new FormatException("NDEF Text thiếu byte trạng thái.");

        var status = payload[0];
        if ((status & 0x40) != 0)
            throw new FormatException("Byte trạng thái NDEF Text có bit dự phòng được đặt.");

        var languageLength = status & 0x3F;
        if (payload.Length < 1 + languageLength)
            throw new FormatException("Độ dài mã ngôn ngữ vượt quá payload.");

        var language = Encoding.ASCII.GetString(payload, 1, languageLength);
        var offset = 1 + languageLength;
        var textBytes = payload.AsSpan(offset).ToArray();
        var isUtf16 = (status & 0x80) != 0;
        if (isUtf16 && textBytes.Length % 2 != 0)
            throw new FormatException("Văn bản UTF-16 có số byte lẻ.");

        string value;
        if (!isUtf16)
            value = StrictUtf8.GetString(textBytes);
        else if (textBytes.Length >= 2 && textBytes[0] == 0xFF && textBytes[1] == 0xFE)
            value = StrictUtf16Le.GetString(textBytes, 2, textBytes.Length - 2);
        else if (textBytes.Length >= 2 && textBytes[0] == 0xFE && textBytes[1] == 0xFF)
            value = StrictUtf16Be.GetString(textBytes, 2, textBytes.Length - 2);
        else
            value = StrictUtf16Be.GetString(textBytes);
        return $"Text ({(isUtf16 ? "UTF-16" : "UTF-8")}, ngôn ngữ {language}): {value}";
    }

    private static string DecodeUri(byte[] payload)
    {
        if (payload.Length == 0)
            throw new FormatException("NDEF URI thiếu byte tiền tố.");
        if (payload[0] >= UriPrefixes.Length)
            throw new FormatException($"Mã tiền tố URI 0x{payload[0]:X2} không hợp lệ.");

        var suffix = StrictUtf8.GetString(payload, 1, payload.Length - 1);
        return $"URI: {UriPrefixes[payload[0]]}{suffix}";
    }

    private static string DecodePlainText(byte[] payload)
    {
        var value = StrictUtf8.GetString(payload);
        if (value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            return "payload có ký tự điều khiển; xem hex ở trên.";
        return value;
    }
}
