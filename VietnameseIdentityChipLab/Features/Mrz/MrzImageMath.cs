namespace VietnameseIdentityChipLab;

/// <summary>Adaptive mean threshold on ARGB grayscale pixels; independent of Android.</summary>
public static class MrzImageMath
{
    public static void AdaptiveThreshold(int[] pixels, int width, int height, int radius = 15)
    {
        if (width <= 0 || height <= 0 || pixels.Length != checked(width * height))
            throw new ArgumentException("Kích thước ảnh không hợp lệ.");
        if (radius < 1) throw new ArgumentOutOfRangeException(nameof(radius));
        var stride = width + 1;
        var integral = new long[checked(stride * (height + 1))];
        for (var y = 0; y < height; y++)
        {
            long rowSum = 0;
            for (var x = 0; x < width; x++)
            {
                rowSum += pixels[y * width + x] & 255;
                integral[(y + 1) * stride + x + 1] = integral[y * stride + x + 1] + rowSum;
            }
        }
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var x1 = Math.Max(0, x - radius);
            var x2 = Math.Min(width, x + radius + 1);
            var y1 = Math.Max(0, y - radius);
            var y2 = Math.Min(height, y + radius + 1);
            var sum = integral[y2 * stride + x2] - integral[y1 * stride + x2] -
                integral[y2 * stride + x1] + integral[y1 * stride + x1];
            var mean = sum / ((x2 - x1) * (y2 - y1));
            pixels[y * width + x] = (pixels[y * width + x] & 255) < mean - 10
                ? unchecked((int)0xFF000000) : unchecked((int)0xFFFFFFFF);
        }
    }
}
