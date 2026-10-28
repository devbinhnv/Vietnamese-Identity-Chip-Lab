using Android.Graphics;
using Android.Media;

namespace VietnameseIdentityChipLab;

/// <summary>In-memory luminance conversion, crop, deskew and adaptive threshold.</summary>
internal static class ImagePreprocessor
{
    // Caller owns the returned bitmap. The camera Image stays open during OCR.
    public static Bitmap FromCameraLuma(Image image, int rotation)
    {
        var plane = image.GetPlanes()![0];
        using var buffer = plane.Buffer!.Duplicate();
        var bytes = new byte[buffer.Remaining()];
        buffer.Get(bytes);
        var width = image.Width;
        var height = image.Height;
        var pixels = new int[width * height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var value = bytes[y * plane.RowStride + x * plane.PixelStride];
            pixels[y * width + x] = unchecked((int)0xFF000000) | value << 16 | value << 8 | value;
        }
        using var bitmap = Bitmap.CreateBitmap(pixels, width, height, Bitmap.Config.Argb8888!)
            ?? throw new InvalidOperationException("Không tạo được ảnh grayscale.");
        using var matrix = new Matrix();
        matrix.PostRotate(rotation);
        // CreateBitmap may return its input for an identity transformation.
        if (rotation == 0)
            return bitmap.Copy(Bitmap.Config.Argb8888!, false)
                ?? throw new InvalidOperationException("Không sao chép được ảnh.");
        return Bitmap.CreateBitmap(bitmap, 0, 0, width, height, matrix, true)
            ?? throw new InvalidOperationException("Không xoay được ảnh camera.");
    }

    /// <summary>Always returns a separately owned bitmap. Does not modify source.</summary>
    public static Bitmap Process(Bitmap source, MrzImageRegion region, bool binarize)
    {
        // Canvas avoids aliased bitmaps when the region covers the entire source.
        using var crop = Bitmap.CreateBitmap(region.Width, region.Height, Bitmap.Config.Argb8888!)
            ?? throw new InvalidOperationException("Không cắt được vùng MRZ.");
        using (var canvas = new Canvas(crop))
            canvas.DrawBitmap(source, -region.Left, -region.Top, null);

        // Enlarge the canvas so rotating does not clip the first or last row.
        var radians = Math.Abs(region.AngleDegrees) * Math.PI / 180;
        var width = (int)Math.Ceiling(crop.Width * Math.Cos(radians) + crop.Height * Math.Sin(radians));
        var height = (int)Math.Ceiling(crop.Width * Math.Sin(radians) + crop.Height * Math.Cos(radians));
        var output = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!)
            ?? throw new InvalidOperationException("Không tạo được ảnh MRZ.");
        try
        {
            using (var canvas = new Canvas(output))
            using (var paint = new Paint(PaintFlags.FilterBitmap))
            {
                canvas.DrawColor(Color.White);
                canvas.Translate(width / 2f, height / 2f);
                canvas.Rotate(-region.AngleDegrees);
                canvas.DrawBitmap(crop, -crop.Width / 2f, -crop.Height / 2f, paint);
            }
            if (binarize)
            {
                var pixels = new int[width * height];
                output.GetPixels(pixels, 0, width, 0, 0, width, height);
                MrzImageMath.AdaptiveThreshold(pixels, width, height);
                output.SetPixels(pixels, 0, width, 0, 0, width, height);
            }
            return output;
        }
        catch { output.Recycle(); output.Dispose(); throw; }
    }
}
