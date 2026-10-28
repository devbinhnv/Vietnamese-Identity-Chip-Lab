using System.Text;
using Android.Nfc;
using Android.Nfc.Tech;

namespace VietnameseIdentityChipLab;

public sealed partial class MainActivity
{
    private string ReadNdefTag(Tag tag)
    {
        using var ndef = Ndef.Get(tag)
            ?? throw new NotSupportedException("Thẻ không có dữ liệu NDEF.");
        try
        {
            ndef.Connect();
            var message = ndef.NdefMessage ?? throw new InvalidOperationException("Thẻ NDEF trống.");
            var records = message.GetRecords() ?? [];
            var output = new StringBuilder();
            for (var i = 0; i < records.Length; i++)
            {
                var record = records[i];
                var payload = record.GetPayload() ?? [];
                output.AppendLine($"Nội dung {i + 1}: {NdefDecoder.Decode(record.Tnf, record.GetTypeInfo(), payload)}");
            }
            return output.ToString().TrimEnd();
        }
        finally { if (ndef.IsConnected) ndef.Close(); }
    }
}
