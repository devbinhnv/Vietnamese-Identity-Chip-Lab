using Android;
using Android.Content.PM;
using Android.Graphics;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;
using Icao9303.Core;

namespace VietnameseIdentityChipLab;

public sealed partial class MainActivity
{
    private const int CameraPermissionRequest = 43;
    private Td1Mrz? _preparedMrz;
    private LiveMrzScanner? _mrzScanner;
    private TextureView? _mrzPreview;
    private Button? _mrzButton;
    private Button? _mrzCaptureButton;
    private Button? _mrzConfirmButton;
    private TextView? _mrzStatus;
    private EditText? _mrzEditable;
    private TextView? _mrzDetails;
    private string _latestRaw = string.Empty;

    private void AddMrzUi(LinearLayout column, int padding)
    {
        var heading = new TextView(this) { Text = "Quét MRZ" };
        heading.SetTextSize(ComplexUnitType.Sp, 22);
        heading.SetPadding(0, padding, 0, 0);
        column.AddView(heading);

        _mrzButton = new Button(this) { Text = "Quét MRZ" };
        _mrzButton.Click += (_, _) => ToggleMrzScan();
        column.AddView(_mrzButton);
        _mrzCaptureButton = new Button(this) { Text = "Chụp thủ công", Visibility = ViewStates.Gone };
        _mrzCaptureButton.Click += (_, _) =>
        {
            _mrzScanner?.CaptureFrame();
            _mrzCaptureButton.Enabled = false;
            SetMrzStatus("Đang nhận dạng…");
        };
        column.AddView(_mrzCaptureButton);
        _mrzPreview = new TextureView(this) { Visibility = ViewStates.Gone };
        _mrzPreview.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent,
            (int)(240 * Resources!.DisplayMetrics!.Density));
        column.AddView(_mrzPreview);

        _mrzStatus = new TextView(this) { Visibility = ViewStates.Gone };
        column.AddView(_mrzStatus);
        _mrzEditable = new EditText(this)
        {
            Hint = "MRZ",
            Visibility = ViewStates.Gone,
            InputType = InputTypes.ClassText | InputTypes.TextFlagMultiLine |
                InputTypes.TextFlagCapCharacters | InputTypes.TextFlagNoSuggestions,
            Typeface = Typeface.Monospace
        };
        _mrzEditable.SetMinLines(3);
        _mrzEditable.SetTextSize(ComplexUnitType.Sp, 14);
        _mrzEditable.TextChanged += (_, _) =>
        {
            Volatile.Write(ref _preparedMrz, null);
            CancelNfcScan();
            UpdateMrzDetails();
        };
        column.AddView(_mrzEditable);

        _mrzDetails = new TextView(this) { Visibility = ViewStates.Gone };
        _mrzDetails.SetTextIsSelectable(true);
        _mrzDetails.SetPadding(0, padding / 2, 0, padding / 2);
        column.AddView(_mrzDetails);

        _mrzConfirmButton = new Button(this)
        {
            Text = "Tiếp tục quét CCCD", Visibility = ViewStates.Gone, Enabled = false
        };
        _mrzConfirmButton.Click += (_, _) =>
        {
            StopMrzScan();
            if (!MrzAccess.TryPrepare(_mrzEditable.Text ?? string.Empty, out var mrz, out _))
            {
                Volatile.Write(ref _preparedMrz, null);
                SetMrzStatus("MRZ chưa hợp lệ. Kiểm tra lại hoặc quét lại.");
                return;
            }
            Volatile.Write(ref _preparedMrz, mrz);
            SetMrzStatus(string.Empty);
            BeginCccdScan();
        };
        column.AddView(_mrzConfirmButton);
    }

    private void UpdateMrzDetails()
    {
        if (_mrzDetails is null || _mrzConfirmButton is null) return;
        var valid = MrzAccess.TryPrepare(_mrzEditable!.Text ?? string.Empty, out var mrz, out _);
        _mrzDetails.Text = valid ? MrzResultFormatter.Format(mrz!) : string.Empty;
        _mrzDetails.Visibility = valid ? ViewStates.Visible : ViewStates.Gone;
        _mrzConfirmButton.Enabled = valid;
        SetMrzStatus(valid ? string.Empty : "MRZ chưa đầy đủ hoặc chưa chính xác.");
    }

    private void ShowMrzResult(string raw)
    {
        // Only MRZ-like rows belong in the result, never surrounding card text or debug output.
        var draft = LiveMrzCandidate.TryFindValidatedTd1(raw, out var candidate) ? candidate :
            string.Join('\n', raw.Replace('\r', '\n').Split('\n')
                .Select(line => string.Concat(line.Where(c => c is not (' ' or '\t'))))
                .Where(line => line.Length is >= 18 and <= 44 &&
                    (line.Contains('<') || line.Count(char.IsAsciiDigit) >= 8) &&
                    line.Count(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '<') >= line.Length * 0.85));
        _mrzEditable!.Visibility = ViewStates.Visible;
        _mrzConfirmButton!.Visibility = ViewStates.Visible;
        _mrzEditable.Text = draft;
        UpdateMrzDetails();
    }

    private void ToggleMrzScan()
    {
        if (_mrzScanner is not null)
        {
            StopMrzScan();
            ShowMrzResult(_latestRaw);
            return;
        }
        if (CheckSelfPermission(Manifest.Permission.Camera) != Permission.Granted)
        {
            RequestPermissions([Manifest.Permission.Camera], CameraPermissionRequest);
            return;
        }
        StartMrzScan();
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != CameraPermissionRequest) return;
        if (grantResults.Length > 0 && grantResults[0] == Permission.Granted) StartMrzScan();
        else SetMrzStatus("Cần cấp quyền camera để quét MRZ.");
    }

    private void StartMrzScan()
    {
        if (_mrzScanner is not null) return;
        _latestRaw = string.Empty;
        Volatile.Write(ref _preparedMrz, null);
        CancelNfcScan();
        _mrzEditable!.Text = string.Empty;
        _mrzEditable.Visibility = ViewStates.Gone;
        _mrzDetails!.Visibility = ViewStates.Gone;
        _mrzConfirmButton!.Visibility = ViewStates.Gone;
        _mrzPreview!.Visibility = ViewStates.Visible;
        try
        {
            _mrzScanner = new LiveMrzScanner(this, _mrzPreview, OnLiveOcr,
                OnLiveOcrError, OnRecoverableOcrError);
            _mrzScanner.Start();
            if (_mrzScanner is null) return;
            _mrzButton!.Text = "Dừng quét";
            _mrzCaptureButton!.Visibility = ViewStates.Visible;
            _mrzCaptureButton.Enabled = true;
            SetMrzStatus("Đang quét MRZ…");
        }
        catch (Exception ex)
        {
            Log.Warn("VietnameseIdentityChipLab", $"Camera: {ex}");
            StopMrzScan();
            SetMrzStatus("Không mở được camera. Hãy thử lại.");
        }
    }

    private void StopMrzScan()
    {
        _mrzScanner?.Stop();
        _mrzScanner = null;
        if (_mrzPreview is not null) _mrzPreview.Visibility = ViewStates.Gone;
        if (_mrzButton is not null) _mrzButton.Text = "Quét MRZ";
        if (_mrzCaptureButton is not null) _mrzCaptureButton.Visibility = ViewStates.Gone;
    }

    private void OnLiveOcr(string raw, bool manual)
    {
        if (_mrzScanner is null) return;
        if (!string.IsNullOrWhiteSpace(raw)) _latestRaw = raw;
        if (!MrzScanDecision.ShouldStop(raw, manual, out var draft)) return;
        StopMrzScan();
        ShowMrzResult(draft);
    }

    private void OnLiveOcrError(string message)
    {
        Log.Warn("VietnameseIdentityChipLab", message);
        StopMrzScan();
        SetMrzStatus("Không quét được MRZ. Hãy thử lại.");
    }

    private void OnRecoverableOcrError(string message)
    {
        if (_mrzScanner is null) return;
        Log.Warn("VietnameseIdentityChipLab", message);
        _mrzCaptureButton!.Enabled = true;
        SetMrzStatus("Chưa đọc được MRZ. Giữ thẻ rõ nét và thử lại.");
    }

    private void SetMrzStatus(string message)
    {
        if (_mrzStatus is null) return;
        _mrzStatus.Text = message;
        _mrzStatus.Visibility = string.IsNullOrEmpty(message) ? ViewStates.Gone : ViewStates.Visible;
    }
}
