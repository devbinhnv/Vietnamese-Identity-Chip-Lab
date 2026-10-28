using VietnameseIdentityChipLab;
using Xunit;

namespace Icao9303.Core.Tests;

public sealed class MrzScanTests
{
    private const string First = "I<UTOD231458907<<<<<<<<<<<<<<<";
    private const string Second = "7408122F1204159UTO<<<<<<<<<<<6";
    private const string Third = "ERIKSSON<<ANNA<MARIA<<<<<<<<<<";

    [Fact]
    public void SingleValidatedFrame_StopsWithoutWaitingForAnotherFrame()
    {
        var valid = $"Heading\n{First}\n{Second}\n{Third}";
        Assert.True(MrzScanDecision.ShouldStop(valid, false, out var draft));
        Assert.Equal($"{First}\n{Second}\n{Third}", draft);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void InvalidOcr_OnlyManualCaptureStopsAndPreservesTheDraft(bool manual, bool shouldStop)
    {
        var raw = $"{First}\n{Second[..6]}O{Second[7..]}\n{Third}";
        Assert.Equal(shouldStop, MrzScanDecision.ShouldStop(raw, manual, out var draft));
        Assert.Equal(raw, draft);
    }

    [Fact]
    public void EmptyManualCapture_StillStopsForManualEntry()
    {
        Assert.True(MrzScanDecision.ShouldStop(string.Empty, true, out var draft));
        Assert.Empty(draft);
    }

    [Fact]
    public void Retry_PreservesUsefulRowsWhenThresholdLosesText()
    {
        var partial = $"{First}\n{Second}";
        Assert.Equal(partial, MrzScanDecision.PreferDraft(partial, "blur"));
        var valid = $"{First}\n{Second}\n{Third}";
        Assert.Equal(valid, MrzScanDecision.PreferDraft(partial, valid));
        Assert.Equal(valid, MrzScanDecision.PreferDraft(valid, partial));
    }

    [Fact]
    public void Region_UsesOcrCoordinatesRatherThanTheBottomOfTheCameraFrame()
    {
        MrzOcrLine[] lines = [new(Third, 100, 180, 700, 200, 3),
            new("DATE OF BIRTH", 100, 40, 400, 60),
            new(First, 100, 100, 700, 120, 3), new(Second, 100, 140, 700, 160, 3)];
        Assert.True(MrzRegionDetector.TryFind(lines, 1080, 1920, out var region));
        Assert.True(region.Left < 100 && region.Left + region.Width > 700);
        Assert.True(region.Top < 100 && region.Top + region.Height > 200);
        Assert.True(region.Top + region.Height < 300);
        Assert.Equal(3f, region.AngleDegrees);
    }

    [Fact]
    public void Region_TwoRowsLeaveRoomForTheMissingRowAndClampToImageBounds()
    {
        MrzOcrLine[] lines = [new(First, 1, 10, 600, 30), new(Second, 1, 50, 600, 70)];
        Assert.True(MrzRegionDetector.TryFind(lines, 610, 200, out var region));
        Assert.Equal(0, region.Left);
        Assert.Equal(0, region.Top);
        Assert.Equal(610, region.Width);
        Assert.True(region.Height >= 110);
    }

    [Fact]
    public void Region_RejectsUnrelatedRowsAndDoesNotInventACardCrop()
    {
        Assert.False(MrzRegionDetector.TryFind([new(First, 10, 10, 600, 30)], 1080, 1920, out _));
        Assert.False(MrzRegionDetector.TryFind([new(First, 10, 10, 600, 30),
            new(Second, 10, 600, 600, 620)], 1080, 1920, out _));
    }

    [Fact]
    public void Threshold_PreservesDarkStrokesOnAnUnevenBackground()
    {
        const int width = 60;
        const int height = 30;
        var pixels = Enumerable.Range(0, width * height).Select(i =>
        {
            var value = 120 + i % width * 2;
            return unchecked((int)0xFF000000) | value << 16 | value << 8 | value;
        }).ToArray();
        for (var y = 5; y < 25; y++)
        {
            pixels[y * width + 15] = unchecked((int)0xFF202020);
            pixels[y * width + 45] = unchecked((int)0xFF202020);
        }
        MrzImageMath.AdaptiveThreshold(pixels, width, height, 5);
        Assert.Equal(unchecked((int)0xFF000000), pixels[15 * width + 15]);
        Assert.Equal(unchecked((int)0xFF000000), pixels[15 * width + 45]);
        Assert.Equal(unchecked((int)0xFFFFFFFF), pixels[15 * width + 30]);
        Assert.Equal(unchecked((int)0xFFFFFFFF), pixels[0]);
        Assert.Equal(unchecked((int)0xFFFFFFFF), pixels[^1]);
    }
}
