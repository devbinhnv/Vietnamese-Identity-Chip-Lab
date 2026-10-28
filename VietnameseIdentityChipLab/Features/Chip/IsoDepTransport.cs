using Android.Nfc.Tech;
using Emrtd;

namespace VietnameseIdentityChipLab;

/// <summary>Bridge between Android NFC and the ICAO APDU protocol.</summary>
public sealed class IsoDepTransport : ICardTransport
{
    private readonly IsoDep _chip;
    private readonly Action<string>? _log;

    public IsoDepTransport(IsoDep chip, Action<string>? log = null)
    {
        _chip = chip;
        _log = log;
        _chip.Connect();
        _chip.Timeout = 10000;
        _log?.Invoke($"IsoDep đã kết nối; APDU tối đa {_chip.MaxTransceiveLength} byte.");
    }

    public byte[] Transmit(byte[] apdu)
    {
        if (apdu.Length < 4)
            throw new ArgumentException("APDU cần ít nhất 4 byte.", nameof(apdu));
        if (apdu.Length > _chip.MaxTransceiveLength)
            throw new IOException($"APDU {apdu.Length} byte vượt giới hạn {_chip.MaxTransceiveLength} byte của thiết bị.");

        // Secure messaging owns the send sequence counter. Retrying a wrapped APDU or
        // issuing a plain GET RESPONSE here would desynchronize that counter and MAC.
        return SendRaw(apdu);
    }

    private byte[] SendRaw(byte[] apdu)
    {
        var response = _chip.Transceive(apdu)
            ?? throw new IOException("IsoDep không trả byte phản hồi.");
        if (response.Length < 2)
            throw new IOException($"Phản hồi APDU chỉ có {response.Length} byte, thiếu mã trạng thái SW.");
        _log?.Invoke($"APDU INS={apdu[1]:X2}: SW={response[^2]:X2}{response[^1]:X2}, {response.Length - 2} byte dữ liệu.");
        return response;
    }

    public void Reset()
    {
        throw new NotSupportedException("Android IsoDep không bảo đảm reset chip trong cùng lần quét. Hãy nhấc thẻ ra và áp lại.");
    }

    public void Dispose()
    {
        try { _chip.Close(); }
        catch (Java.IO.IOException) { }
    }
}
