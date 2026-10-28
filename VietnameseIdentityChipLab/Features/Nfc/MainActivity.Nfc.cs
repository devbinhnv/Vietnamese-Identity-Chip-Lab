using Android.Graphics;
using Android.Nfc;
using Android.Nfc.Tech;
using Android.Util;
using Android.Views;

namespace VietnameseIdentityChipLab;

public sealed partial class MainActivity
{
    private NfcAdapter? _adapter;
    private bool _resumed;
    private volatile bool _nfcRequested;
    private int _nfcGeneration;
    private int _reading;
    private IsoDep? _activeChip;

    protected override void OnResume()
    {
        base.OnResume();
        _resumed = true;
        if (_nfcRequested) ActivateNfcReader();
    }

    protected override void OnPause()
    {
        StopMrzScan();
        _resumed = false;
        if (_nfcRequested)
        {
            CancelNfcScan();
            ShowStatus("Đã tạm dừng quét NFC. Nhấc thẻ và nhấn quét lại.");
            if (_nfcRetryButton is not null) _nfcRetryButton.Visibility = ViewStates.Visible;
        }
        base.OnPause();
    }

    private void CancelNfcScan()
    {
        _nfcRequested = false;
        Interlocked.Increment(ref _nfcGeneration);
        DisableNfcReader();
        var chip = Interlocked.Exchange(ref _activeChip, null);
        try { chip?.Close(); }
        catch (Exception ex) { Log.Debug("VietnameseIdentityChipLab", $"Close NFC: {ex.Message}"); }
        if (_nfcProgress is not null) _nfcProgress.Visibility = ViewStates.Gone;
        if (_nfcRetryButton is not null) _nfcRetryButton.Visibility = ViewStates.Gone;
        if (_output is not null) { _output.Text = string.Empty; _output.Visibility = ViewStates.Gone; }
        SetFaceImage(null);
        _status?.SetTextColor(Color.DarkGray);
        ShowStatus("Xác nhận MRZ để bắt đầu quét CCCD.");
    }

    private void BeginCccdScan()
    {
        if (Volatile.Read(ref _preparedMrz) is null)
        {
            ShowStatus("Cần quét và xác nhận MRZ trước khi đọc CCCD.");
            return;
        }
        _mrzSection!.Visibility = ViewStates.Gone;
        _nfcSection!.Visibility = ViewStates.Visible;
        _scroll?.SmoothScrollTo(0, 0);
        if (Volatile.Read(ref _reading) != 0)
        {
            ShowStatus("Đang kết thúc kết nối trước. Nhấc thẻ rồi nhấn quét lại.");
            _nfcRetryButton!.Visibility = ViewStates.Visible;
            return;
        }
        CancelNfcScan();
        _nfcRequested = true;
        ActivateNfcReader();
    }

    private void ActivateNfcReader()
    {
        var generation = Volatile.Read(ref _nfcGeneration);
        if (_adapter is null)
        {
            FinishNfcScan(generation, 0, "Thiết bị không hỗ trợ NFC.", failed: true);
            return;
        }
        if (!_adapter.IsEnabled)
        {
            FinishNfcScan(generation, 0, "NFC đang tắt. Bật NFC trong Cài đặt, rồi nhấn quét lại.", failed: true);
            return;
        }
        if (!_resumed) return;
        try
        {
            var flags = NfcReaderFlags.NfcA | NfcReaderFlags.NfcB | NfcReaderFlags.SkipNdefCheck;
            _adapter.EnableReaderMode(this, this, flags, null);
            UpdateNfcProgress(generation, 0, "Áp CCCD vào mặt sau điện thoại và giữ yên.");
        }
        catch (Exception ex)
        {
            Log.Warn("VietnameseIdentityChipLab", $"Enable reader: {ex}");
            FinishNfcScan(generation, 0, "Không bật được chế độ quét NFC. Hãy thử lại.", failed: true);
        }
    }

    private void DisableNfcReader()
    {
        try { _adapter?.DisableReaderMode(this); }
        catch (Exception ex) { Log.Debug("VietnameseIdentityChipLab", $"Disable reader: {ex.Message}"); }
    }

    private bool IsNfcScanCurrent(int generation) =>
        _nfcRequested && generation == Volatile.Read(ref _nfcGeneration);

    private void UpdateNfcProgress(int generation, int stage, string message) => RunOnUiThread(() =>
    {
        if (!IsNfcScanCurrent(generation)) return;
        _status?.SetTextColor(Color.DarkGray);
        ShowStatus(message);
        _nfcProgress!.Visibility = ViewStates.Visible;
        Log.Debug("VietnameseIdentityChipLab", NfcReadFeedback.Checklist(stage));
    });

    private void FinishNfcScan(int generation, int stage, string message, string result = "",
        bool failed = false, bool matches = true, bool photoAvailable = false) => RunOnUiThread(() =>
    {
        if (!IsNfcScanCurrent(generation)) return;
        _nfcRequested = false;
        DisableNfcReader();
        _status?.SetTextColor(failed || !matches ? Color.Rgb(170, 40, 40) : Color.Rgb(30, 110, 60));
        ShowStatus(message);
        _nfcProgress!.Visibility = ViewStates.Gone;
        Log.Debug("VietnameseIdentityChipLab", NfcReadFeedback.Checklist(stage, failed, photoAvailable));
        _output!.Text = result;
        _output.Visibility = string.IsNullOrEmpty(result) ? ViewStates.Gone : ViewStates.Visible;
        _nfcRetryButton!.Visibility = ViewStates.Visible;
    });

    public void OnTagDiscovered(Tag? tag)
    {
        if (tag is null || !_nfcRequested || Interlocked.Exchange(ref _reading, 1) == 1) return;
        var generation = Volatile.Read(ref _nfcGeneration);
        try
        {
            if (!IsNfcScanCurrent(generation)) return;
            UpdateNfcProgress(generation, 1, "Đã nhận thẻ. Đang kết nối…");
            var prepared = Volatile.Read(ref _preparedMrz);
            if (prepared is null)
            {
                FinishNfcScan(generation, 1, "MRZ đã thay đổi. Quay lại MRZ để xác nhận.", failed: true);
                return;
            }
            using var chip = IsoDep.Get(tag);
            if (chip is null)
            {
                FinishNfcScan(generation, 1,
                    "Không kết nối được chip CCCD. Nhấc thẻ rồi thử lại.", failed: true);
                return;
            }
            Interlocked.Exchange(ref _activeChip, chip);
            if (IsNfcScanCurrent(generation)) ReadIdentityChip(chip, prepared, generation);
        }
        catch (Exception ex)
        {
            Log.Warn("VietnameseIdentityChipLab", $"NFC: {ex}");
            FinishNfcScan(generation, 1, "Mất kết nối với thẻ. Nhấc thẻ, áp lại và giữ yên.", failed: true);
        }
        finally
        {
            Interlocked.Exchange(ref _activeChip, null);
            Interlocked.Exchange(ref _reading, 0);
        }
    }
}
