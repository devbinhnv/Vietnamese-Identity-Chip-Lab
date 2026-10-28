using Android.App;
using Android.Graphics;
using Android.Nfc;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace VietnameseIdentityChipLab;

[Activity(Label = "Vietnamese Identity Chip Lab", MainLauncher = true, Exported = true)]
public sealed partial class MainActivity : Activity, NfcAdapter.IReaderCallback
{
    private TextView? _status;
    private TextView? _output;
    private ImageView? _faceImage;
    private Bitmap? _faceBitmap;
    private ScrollView? _scroll;
    private LinearLayout? _mrzSection;
    private LinearLayout? _nfcSection;
    private ProgressBar? _nfcProgress;
    private Button? _nfcRetryButton;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        try
        {
            BuildMainScreen();
        }
        catch (Exception ex)
        {
            Log.Error("VietnameseIdentityChipLab", $"Startup failed: {ex}");
            var error = new TextView(this)
            {
                Text = $"Không khởi tạo được giao diện.\n{ex.GetType().Name}: {ex.Message}\n\nHãy gửi nội dung này để xác định lỗi."
            };
            error.SetPadding(24, 24, 24, 24);
            SetContentView(error);
        }
    }

    private void BuildMainScreen()
    {
        var padding = (int)(16 * Resources!.DisplayMetrics!.Density);
        var column = new LinearLayout(this) { Orientation = Orientation.Vertical };
        column.SetPadding(padding, padding, padding, padding);

        var title = new TextView(this) { Text = "Vietnamese Identity Chip Lab" };
        title.SetTextSize(Android.Util.ComplexUnitType.Sp, 22);
        column.AddView(title);

        _mrzSection = new LinearLayout(this) { Orientation = Orientation.Vertical };
        column.AddView(_mrzSection);
        AddMrzUi(_mrzSection, padding);

        _nfcSection = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical, Visibility = ViewStates.Gone
        };
        column.AddView(_nfcSection);
        var nfcHeading = new TextView(this) { Text = "Quét CCCD" };
        nfcHeading.SetTextSize(Android.Util.ComplexUnitType.Sp, 22);
        nfcHeading.SetPadding(0, padding, 0, 0);
        _nfcSection.AddView(nfcHeading);

        _status = new TextView(this) { Text = "Xác nhận MRZ để bắt đầu quét CCCD." };
        _status.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);
        _nfcSection.AddView(_status);
        _nfcProgress = new ProgressBar(this) { Indeterminate = true, Visibility = ViewStates.Gone };
        var spinnerSize = (int)(32 * Resources.DisplayMetrics.Density);
        _nfcSection.AddView(_nfcProgress, new LinearLayout.LayoutParams(spinnerSize, spinnerSize));
        _nfcRetryButton = new Button(this) { Text = "Quét lại CCCD", Visibility = ViewStates.Gone };
        _nfcRetryButton.Click += (_, _) => BeginCccdScan();
        _nfcSection.AddView(_nfcRetryButton);

        _output = new TextView(this) { Visibility = ViewStates.Gone };
        _output.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
        _output.SetTextIsSelectable(true);
        _nfcSection.AddView(_output);

        _faceImage = new ImageView(this);
        _faceImage.SetAdjustViewBounds(true);
        _faceImage.Visibility = ViewStates.Gone;
        _nfcSection.AddView(_faceImage);

        var backButton = new Button(this) { Text = "Quay lại MRZ" };
        backButton.Click += (_, _) => ShowMrzStep();
        _nfcSection.AddView(backButton);

        _scroll = new ScrollView(this);
        _scroll.AddView(column);
        SetContentView(_scroll);

        try
        {
            _adapter = NfcAdapter.GetDefaultAdapter(this);
        }
        catch (Exception ex)
        {
            Log.Error("VietnameseIdentityChipLab", $"NFC adapter initialization failed: {ex}");
            ShowStatus($"Không khởi tạo được NFC: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void ShowStatus(string message)
    {
        if (_status is not null) _status.Text = message;
    }

    private void ShowMrzStep()
    {
        Volatile.Write(ref _preparedMrz, null);
        CancelNfcScan();
        _nfcSection!.Visibility = ViewStates.Gone;
        _mrzSection!.Visibility = ViewStates.Visible;
        UpdateMrzDetails();
        _scroll?.SmoothScrollTo(0, 0);
    }

    private void SetFaceImage(Bitmap? bitmap)
    {
        _faceImage?.SetImageBitmap(bitmap);
        if (_faceImage is not null)
            _faceImage.Visibility = bitmap is null ? ViewStates.Gone : ViewStates.Visible;
        var previous = _faceBitmap;
        _faceBitmap = bitmap;
        previous?.Recycle();
        previous?.Dispose();
    }

    protected override void OnDestroy()
    {
        CancelNfcScan();
        SetFaceImage(null);
        base.OnDestroy();
    }
}
