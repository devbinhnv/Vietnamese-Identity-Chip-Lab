using System.Security.Cryptography;
using Android.Graphics;
using Android.Nfc.Tech;
using Android.Util;
using Android.Views;
using Emrtd;
using Icao9303.Core;

namespace VietnameseIdentityChipLab;

public sealed partial class MainActivity
{
    private void ReadIdentityChip(IsoDep chip, Td1Mrz confirmedMrz, int generation)
    {
        var stage = "kết nối";
        var stageIndex = 1;
        try
        {
            using var transport = new IsoDepTransport(chip);
            var session = new IcaoChipSession(transport);
            if (!IsNfcScanCurrent(generation)) return;

            stage = "xác thực truy cập";
            stageIndex = 2;
            UpdateNfcProgress(generation, stageIndex, "Đã kết nối chip. Đang xác thực truy cập… Giữ yên thẻ.");
            byte[]? cardAccess = null;
            try { cardAccess = session.ReadCardAccess(); }
            catch (CardException) { /* CardAccess may be absent on a BAC-only chip. */ }
            var pace = cardAccess is null ? null : PaceInfo.ParseCardAccess(cardAccess)
                .FirstOrDefault(info => info.IsSupported && info.Mapping == PaceMapping.GenericMapping);
            if (!IsNfcScanCurrent(generation)) return;
            if (pace is not null) session.AuthenticatePace(confirmedMrz, pace);
            else session.AuthenticateBac(confirmedMrz);

            stage = "đọc thông tin";
            stageIndex = 3;
            UpdateNfcProgress(generation, stageIndex, "Xác thực thành công. Đang đọc thông tin CCCD…");
            var dg1 = session.ReadDg1Raw();
            var result = ChipMrzResult.FromDg1(IcaoChipSession.DecodeDg1(dg1), confirmedMrz);
            var details = "Thông tin đọc từ chip\n" + MrzResultFormatter.Format(result.Mrz);
            RunOnUiThread(() =>
            {
                if (!IsNfcScanCurrent(generation)) return;
                _output!.Text = details;
                _output.Visibility = ViewStates.Visible;
            });

            stage = "đọc ảnh chân dung";
            stageIndex = 4;
            UpdateNfcProgress(generation, stageIndex, result.MatchesConfirmed
                ? "Đã đọc thông tin và khớp MRZ. Đang đọc ảnh chân dung…"
                : "Thông tin trong chip khác MRZ. Đang đọc ảnh chân dung…");
            if (!IsNfcScanCurrent(generation)) return;
            var photoAvailable = false;
            var photoNote = "Chip chưa cung cấp ảnh chân dung.";
            try
            {
                var dg2 = session.ReadDg2Raw();
                if (dg2 is not null)
                {
                    var face = IcaoChipSession.DecodeDg2(dg2.Value);
                    if (face?.MimeType == "image/jpeg")
                    {
                        var bytes = face.Bytes.ToArray();
                        var bitmap = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
                        photoAvailable = bitmap is not null;
                        photoNote = bitmap is null ? "Không hiển thị được ảnh chân dung." : string.Empty;
                        RunOnUiThread(() =>
                        {
                            if (IsNfcScanCurrent(generation)) SetFaceImage(bitmap);
                            else { bitmap?.Recycle(); bitmap?.Dispose(); }
                        });
                    }
                    else photoNote = "Định dạng ảnh chân dung chưa được hỗ trợ.";
                }
            }
            catch (Exception ex) when (ex is CardException or FormatException or CryptographicException or Java.IO.IOException or IOException)
            {
                Log.Warn("VietnameseIdentityChipLab", $"Read portrait: {ex}");
                FinishNfcScan(generation, stageIndex,
                    result.MatchesConfirmed
                        ? "Đã đọc thông tin và khớp MRZ; lỗi khi đọc ảnh. Nhấc thẻ và quét lại để thử đọc ảnh."
                        : "Thông tin trong chip khác MRZ; đọc ảnh thất bại. Kiểm tra lại giấy tờ.",
                    details, failed: true, matches: result.MatchesConfirmed);
                return;
            }
            FinishNfcScan(generation, 5, result.CompletionMessage(photoAvailable),
                details + (string.IsNullOrEmpty(photoNote) ? string.Empty : "\n\n" + photoNote),
                matches: result.MatchesConfirmed, photoAvailable: photoAvailable);
        }
        catch (Exception ex)
        {
            Log.Warn("VietnameseIdentityChipLab", $"NFC at {stage}: {ex}");
            var reason = ex is Java.IO.IOException or IOException
                ? "Mất kết nối với thẻ."
                : stageIndex == 2 ? "Không xác thực được truy cập chip. Kiểm tra lại MRZ."
                : "Chưa đọc được dữ liệu CCCD hợp lệ.";
            FinishNfcScan(generation, stageIndex,
                $"{reason}\nDừng ở bước {stage}. Nhấc thẻ, áp lại và giữ yên để thử lại.", failed: true);
        }
    }
}
