using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Views;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;

namespace VietnameseIdentityChipLab;

/// <summary>Camera2 preview with one bounded ML Kit request at a time.</summary>
internal sealed class LiveMrzScanner : Java.Lang.Object, TextureView.ISurfaceTextureListener,
    ImageReader.IOnImageAvailableListener
{
    private const long RecognitionIntervalMs = 200;
    private readonly Activity _activity;
    private readonly TextureView _preview;
    private readonly Action<string, bool> _onText;
    private readonly Action<string> _onFatalError;
    private readonly Action<string> _onRecognitionError;
    private readonly object _gate = new();

    private ITextRecognizer? _recognizer;
    private HandlerThread? _imageThread;
    private Handler? _imageHandler;
    private CameraDevice? _device;
    private CameraCaptureSession? _session;
    private ImageReader? _reader;
    private Surface? _previewSurface;
    private bool _running;
    private bool _opening;
    private bool _processing;
    private bool _manualRequested;
    private long _lastRecognitionAt;
    private int _sensorOrientation;
    private int _width;
    private int _height;

    public LiveMrzScanner(Activity activity, TextureView preview, Action<string, bool> onText,
        Action<string> onFatalError, Action<string> onRecognitionError)
    {
        _activity = activity;
        _preview = preview;
        _onText = onText;
        _onFatalError = onFatalError;
        _onRecognitionError = onRecognitionError;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_running) return;
            _recognizer = TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions);
            _imageThread = new HandlerThread("mrz-frames");
            _imageThread.Start();
            _imageHandler = new Handler(_imageThread.Looper!);
            _running = true;
        }
        _preview.SurfaceTextureListener = this;
        if (_preview.IsAvailable) OpenCamera();
    }

    public void CaptureFrame()
    {
        lock (_gate)
            if (_running) _manualRequested = true;
    }

    public void Stop()
    {
        _preview.SurfaceTextureListener = null;
        lock (_gate)
        {
            _running = false;
            _manualRequested = false;
        }
        CloseCamera();
        _imageThread?.QuitSafely();
        _imageHandler?.Dispose();
        _imageHandler = null;
        _imageThread?.Dispose();
        _imageThread = null;
        ReleaseRecognitionResourcesIfIdle();
    }

    public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height) => OpenCamera();
    public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height) => ConfigureTransform();
    public bool OnSurfaceTextureDestroyed(SurfaceTexture surface) { Stop(); return true; }
    public void OnSurfaceTextureUpdated(SurfaceTexture surface) { }

    private void OpenCamera()
    {
        lock (_gate)
        {
            if (!_running || _opening || _device is not null) return;
            _opening = true;
        }
        try
        {
            var manager = (CameraManager?)_activity.GetSystemService(Context.CameraService)
                ?? throw new InvalidOperationException("Không tìm thấy CameraManager.");
            var id = manager.GetCameraIdList().FirstOrDefault(cameraId =>
            {
                var info = manager.GetCameraCharacteristics(cameraId);
                return ((Java.Lang.Integer?)info.Get(CameraCharacteristics.LensFacing))?.IntValue()
                    == (int)LensFacing.Back;
            }) ?? throw new InvalidOperationException("Không tìm thấy camera sau.");

            var characteristics = manager.GetCameraCharacteristics(id);
            _sensorOrientation = ((Java.Lang.Integer?)characteristics.Get(
                CameraCharacteristics.SensorOrientation))?.IntValue() ?? 90;
            var map = (StreamConfigurationMap?)characteristics.Get(
                CameraCharacteristics.ScalerStreamConfigurationMap)
                ?? throw new InvalidOperationException("Camera không cung cấp cấu hình luồng.");
            var previewSizes = map.GetOutputSizes(Java.Lang.Class.FromType(typeof(SurfaceTexture)))
                ?? throw new InvalidOperationException("Camera không hỗ trợ preview.");
            var size = map.GetOutputSizes((int)ImageFormatType.Yuv420888)?
                .Where(s => previewSizes.Any(p => p.Width == s.Width && p.Height == s.Height))
                .OrderBy(s => Math.Max(s.Width, s.Height) <= 1920 && Math.Min(s.Width, s.Height) <= 1080 ? 0 : 1)
                .ThenBy(s => Math.Abs(Math.Max(s.Width, s.Height) - 1920) + Math.Abs(Math.Min(s.Width, s.Height) - 1080))
                .FirstOrDefault() ?? throw new InvalidOperationException("Camera không có kích thước chung cho preview và OCR.");

            _width = size.Width;
            _height = size.Height;
            // One image remains with ML Kit; AcquireLatestImage needs two free slots.
            _reader = ImageReader.NewInstance(_width, _height, ImageFormatType.Yuv420888, 3);
            _reader.SetOnImageAvailableListener(this, _imageHandler);
            ResizePreview();
            manager.OpenCamera(id, new CameraState(this), null);
        }
        catch (Exception ex)
        {
            lock (_gate) _opening = false;
            CloseCamera();
            Fatal($"Mở camera: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private int RotationDegrees => _activity.WindowManager?.DefaultDisplay?.Rotation switch
    {
        SurfaceOrientation.Rotation90 => 90,
        SurfaceOrientation.Rotation180 => 180,
        SurfaceOrientation.Rotation270 => 270,
        _ => 0
    };

    private int ImageRotation => (_sensorOrientation - RotationDegrees + 360) % 360;

    private void ResizePreview()
    {
        var width = _preview.Width;
        if (width <= 0) return;
        var rotated = ImageRotation is 90 or 270;
        var displayWidth = rotated ? _height : _width;
        var displayHeight = rotated ? _width : _height;
        var height = (int)Math.Round(width * (double)displayHeight / displayWidth);
        var parameters = _preview.LayoutParameters;
        if (parameters is null || parameters.Height == height) return;
        parameters.Height = height;
        _preview.LayoutParameters = parameters;
    }

    private void ConfigureTransform()
    {
        if (_width == 0 || _height == 0 || _preview.Width == 0 || _preview.Height == 0) return;
        var viewWidth = _preview.Width;
        var viewHeight = _preview.Height;
        using var matrix = new Matrix();
        var rotation = _activity.WindowManager?.DefaultDisplay?.Rotation ?? SurfaceOrientation.Rotation0;
        var centerX = viewWidth / 2f;
        var centerY = viewHeight / 2f;
        if (rotation is SurfaceOrientation.Rotation90 or SurfaceOrientation.Rotation270)
        {
            using var viewRect = new RectF(0, 0, viewWidth, viewHeight);
            using var bufferRect = new RectF(0, 0, _height, _width);
            bufferRect.Offset(centerX - bufferRect.CenterX(), centerY - bufferRect.CenterY());
            matrix.SetRectToRect(viewRect, bufferRect, Matrix.ScaleToFit.Fill);
            var scale = Math.Max((float)viewHeight / _height, (float)viewWidth / _width);
            matrix.PostScale(scale, scale, centerX, centerY);
            matrix.PostRotate(rotation == SurfaceOrientation.Rotation90 ? -90 : 90, centerX, centerY);
        }
        else if (rotation == SurfaceOrientation.Rotation180)
            matrix.PostRotate(180, centerX, centerY);
        _preview.SetTransform(matrix);
    }

    private void ConfigureSession(CameraDevice device)
    {
        if (!_preview.IsAvailable || _reader is null) { device.Close(); return; }
        try
        {
            var texture = _preview.SurfaceTexture
                ?? throw new InvalidOperationException("Preview chưa sẵn sàng.");
            texture.SetDefaultBufferSize(_width, _height);
            ConfigureTransform();
            _previewSurface = new Surface(texture);
            if (OperatingSystem.IsAndroidVersionAtLeast(28))
            {
                var outputs = new List<OutputConfiguration>
                {
                    new(_previewSurface), new(_reader.Surface!)
                };
                var configuration = new SessionConfiguration(0, outputs, _activity.MainExecutor!,
                    new SessionState(this, device));
                device.CreateCaptureSession(configuration);
            }
            else
            {
#pragma warning disable CA1422
                device.CreateCaptureSession([_previewSurface, _reader.Surface!],
                    new SessionState(this, device), null);
#pragma warning restore CA1422
            }
        }
        catch (Exception ex) { Fatal($"Preview: {ex.GetType().Name}: {ex.Message}"); }
    }

    private void StartRepeating(CameraCaptureSession session, CameraDevice device)
    {
        if (_reader is null || _previewSurface is null) return;
        try
        {
            var request = device.CreateCaptureRequest(CameraTemplate.Preview);
            request.AddTarget(_previewSurface);
            request.AddTarget(_reader.Surface!);
            request.Set(CaptureRequest.ControlAfMode!, (int)ControlAFMode.ContinuousPicture);
            session.SetRepeatingRequest(request.Build(), null, null);
        }
        catch (Exception ex) { Fatal($"Luồng camera: {ex.GetType().Name}: {ex.Message}"); }
    }

    public void OnImageAvailable(ImageReader? reader)
    {
        if (reader is null) return;
        Image? image;
        try { image = reader.AcquireLatestImage(); }
        catch (Exception) { return; } // Reader may close while the activity pauses.
        if (image is null) return;

        ITextRecognizer? recognizer;
        bool manual;
        lock (_gate)
        {
            var now = SystemClock.ElapsedRealtime();
            if (!_running || _processing ||
                (!_manualRequested && now - _lastRecognitionAt < RecognitionIntervalMs))
            {
                image.Close();
                return;
            }
            recognizer = _recognizer;
            if (recognizer is null) { image.Close(); return; }
            manual = _manualRequested;
            _manualRequested = false;
            _processing = true;
            _lastRecognitionAt = now;
        }

        var frame = new FrameRequest(image, manual, ImageRotation);
        try
        {
            frame.Input = InputImage.FromMediaImage(image, frame.Rotation);
            Recognize(recognizer, frame);
        }
        catch (Exception ex)
        {
            FinishFrame(frame);
            RecognitionError($"ML Kit: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Recognize(ITextRecognizer recognizer, FrameRequest frame)
    {
        var callback = new RecognitionCallback(this, frame);
        var task = recognizer.Process(frame.Input!);
        task.AddOnSuccessListener(callback);
        task.AddOnFailureListener(callback);
    }

    private void FinishFrame(FrameRequest frame)
    {
        if (!frame.TryDispose()) return;
        lock (_gate) _processing = false;
        ReleaseRecognitionResourcesIfIdle();
    }

    private void CompleteFrame(FrameRequest frame, string raw)
    {
        FinishFrame(frame);
        _activity.RunOnUiThread(() =>
        {
            lock (_gate) { if (!_running) return; }
            _onText(raw, frame.Manual);
        });
    }

    private void RetryPreprocessed(FrameRequest frame, Text? text)
    {
        if (frame.Attempt == 0)
        {
            var width = frame.Rotation is 90 or 270 ? frame.Image.Height : frame.Image.Width;
            var height = frame.Rotation is 90 or 270 ? frame.Image.Width : frame.Image.Height;
            var observations = text?.TextBlocks.SelectMany(b => b.Lines).Select(line =>
            {
                var box = line.BoundingBox;
                var corners = line.GetCornerPoints();
                var angle = corners is { Length: >= 2 }
                    ? (float)(Math.Atan2(corners[1].Y - corners[0].Y, corners[1].X - corners[0].X) * 180 / Math.PI)
                    : 0;
                return new MrzOcrLine(line.Text ?? string.Empty, box?.Left ?? 0, box?.Top ?? 0,
                    box?.Right ?? 0, box?.Bottom ?? 0, angle);
            }).ToArray() ?? [];
            frame.Region = MrzRegionDetector.TryFind(observations, width, height, out var region)
                ? region : new(0, 0, width, height, 0);
        }

        Handler? handler;
        lock (_gate) handler = _running ? _imageHandler : null;
        if (handler is null) { FinishFrame(frame); return; }
        // ML Kit listeners run on the main thread; pixel work must return to the frame thread.
        if (!handler.Post(() =>
        {
            ITextRecognizer? recognizer;
            lock (_gate) recognizer = _running ? _recognizer : null;
            if (recognizer is null) { FinishFrame(frame); return; }
            try
            {
                frame.ClearInput();
                frame.Grayscale ??= ImagePreprocessor.FromCameraLuma(frame.Image, frame.Rotation);
                frame.Attempt++;
                frame.Processed = ImagePreprocessor.Process(frame.Grayscale, frame.Region,
                    binarize: frame.Attempt == 2);
                frame.Input = InputImage.FromBitmap(frame.Processed, 0);
                Recognize(recognizer, frame);
            }
            catch (Exception ex)
            {
                CompleteFrame(frame, frame.BestRaw);
                RecognitionError($"Xử lý ảnh: {ex.GetType().Name}: {ex.Message}");
            }
        })) FinishFrame(frame);
    }

    private void CloseCamera()
    {
        _session?.Close();
        _session = null;
        _device?.Close();
        _device = null;
        _previewSurface?.Release();
        _previewSurface = null;
        lock (_gate)
        {
            _opening = false;
            if (!_processing) { _reader?.Close(); _reader = null; }
        }
    }

    private void ReleaseRecognitionResourcesIfIdle()
    {
        lock (_gate)
        {
            if (_running || _processing) return;
            _reader?.Close();
            _reader = null;
            _recognizer?.Close();
            _recognizer = null;
        }
    }

    private void Fatal(string message) => _activity.RunOnUiThread(() =>
    {
        lock (_gate) { if (!_running) return; }
        _onFatalError(message);
    });

    private void RecognitionError(string message) => _activity.RunOnUiThread(() =>
    {
        lock (_gate) { if (!_running) return; }
        _onRecognitionError(message);
    });

    private sealed class CameraState(LiveMrzScanner owner) : CameraDevice.StateCallback
    {
        public override void OnOpened(CameraDevice camera)
        {
            lock (owner._gate)
            {
                owner._opening = false;
                if (!owner._running) { camera.Close(); return; }
                owner._device = camera;
            }
            owner.ConfigureSession(camera);
        }
        public override void OnDisconnected(CameraDevice camera)
        {
            camera.Close();
            owner.Fatal("Camera đã ngắt kết nối.");
        }
        public override void OnError(CameraDevice camera, CameraError error)
        {
            camera.Close();
            owner.Fatal($"Camera lỗi {error}.");
        }
    }

    private sealed class SessionState(LiveMrzScanner owner, CameraDevice device)
        : CameraCaptureSession.StateCallback
    {
        public override void OnConfigured(CameraCaptureSession session)
        {
            lock (owner._gate)
            {
                if (!owner._running) { session.Close(); return; }
                owner._session = session;
            }
            owner.StartRepeating(session, device);
        }
        public override void OnConfigureFailed(CameraCaptureSession session) =>
            owner.Fatal("Không tạo được phiên camera.");
    }

    private sealed class FrameRequest(Image image, bool manual, int rotation)
    {
        private int _disposed;
        public Image Image { get; } = image;
        public bool Manual { get; } = manual;
        public int Rotation { get; } = rotation;
        public int Attempt { get; set; }
        public string BestRaw { get; set; } = string.Empty;
        public MrzImageRegion Region { get; set; }
        public InputImage? Input { get; set; }
        public Bitmap? Grayscale { get; set; }
        public Bitmap? Processed { get; set; }

        public void ClearInput()
        {
            Input?.Dispose();
            Input = null;
            Processed?.Recycle();
            Processed?.Dispose();
            Processed = null;
        }

        public bool TryDispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return false;
            ClearInput();
            Grayscale?.Recycle();
            Grayscale?.Dispose();
            Grayscale = null;
            Image.Close();
            Image.Dispose();
            return true;
        }
    }

    private sealed class RecognitionCallback(LiveMrzScanner owner, FrameRequest frame)
        : Java.Lang.Object, Android.Gms.Tasks.IOnSuccessListener, Android.Gms.Tasks.IOnFailureListener
    {
        public void OnSuccess(Java.Lang.Object? result)
        {
            try
            {
                var text = result as Text;
                var raw = text?.GetText() ?? string.Empty;
                // ML Kit may split the three rows across blocks. Preserve each row's characters.
                if (!LiveMrzCandidate.TryFindValidatedTd1(raw, out _) && text is not null)
                {
                    var ordered = string.Join('\n', text.TextBlocks.SelectMany(b => b.Lines)
                        .OrderBy(l => l.BoundingBox?.Top ?? 0).ThenBy(l => l.BoundingBox?.Left ?? 0)
                        .Select(l => l.Text));
                    if (LiveMrzCandidate.TryFindValidatedTd1(ordered, out _)) raw = ordered;
                }
                if (!string.IsNullOrWhiteSpace(raw))
                    frame.BestRaw = MrzScanDecision.PreferDraft(frame.BestRaw, raw);
                if (LiveMrzCandidate.TryFindValidatedTd1(raw, out _) || frame.Attempt >= 2)
                    owner.CompleteFrame(frame, frame.BestRaw);
                else
                    owner.RetryPreprocessed(frame, text);
            }
            catch (Exception ex)
            {
                owner.FinishFrame(frame);
                owner.RecognitionError($"ML Kit: {ex.GetType().Name}: {ex.Message}");
            }
        }
        public void OnFailure(Java.Lang.Exception error)
        {
            // Keep the last OCR draft available for manual review if a later retry fails.
            owner.CompleteFrame(frame, frame.BestRaw);
            owner.RecognitionError($"ML Kit: {error.Message}");
        }
    }
}
