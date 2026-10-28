using VietnameseIdentityChipLab;
using Xunit;

namespace Icao9303.Core.Tests;

public sealed class ChipReadResultTests
{
    private const string First = "I<UTOD231458907<<<<<<<<<<<<<<<";
    private const string Second = "7408122F1204159UTO<<<<<<<<<<<6";
    private const string Third = "ERIKSSON<<ANNA<MARIA<<<<<<<<<<";
    private static Td1Mrz Confirmed => Td1Mrz.ParseValidated(First, Second, Third);

    [Fact]
    public void SuccessfulRead_RequiresValidatedChipDataAndReportsMissingPortraitSeparately()
    {
        var result = ChipMrzResult.FromDg1([First, Second, Third], Confirmed);
        Assert.True(result.MatchesConfirmed);
        Assert.Contains("thành công", result.CompletionMessage(true));
        Assert.Contains("Chưa hiển thị được ảnh", result.CompletionMessage(false));
        Assert.DoesNotContain("Đọc CCCD thành công", result.CompletionMessage(false));
    }

    [Fact]
    public void DifferentUnchecksummedName_IsStillAMismatch()
    {
        var chipName = Third.Replace("ANNA", "ANNE", StringComparison.Ordinal);
        var result = ChipMrzResult.FromDg1([First, Second, chipName], Confirmed);
        Assert.False(result.MatchesConfirmed);
        Assert.Equal(chipName, result.Mrz.Third);
        Assert.Contains("khác MRZ", result.CompletionMessage(true));
        Assert.DoesNotContain("thành công", result.CompletionMessage(true));
    }

    [Fact]
    public void DetectedTagOrMalformedDg1_IsNotASuccessfulChipRead()
    {
        Assert.Throws<FormatException>(() => ChipMrzResult.FromDg1([], Confirmed));
        Assert.Throws<FormatException>(() => ChipMrzResult.FromDg1([First, Second[..29] + "0", Third], Confirmed));
    }

    [Fact]
    public void AuthenticationFailure_DoesNotMarkDataOrPortraitAsRead()
    {
        var feedback = NfcReadFeedback.Checklist(2, failed: true);
        Assert.Contains("Nhận thẻ: xong", feedback);
        Assert.Contains("Kết nối: xong", feedback);
        Assert.Contains("Xác thực truy cập: lỗi", feedback);
        Assert.Contains("Đọc thông tin: chưa thực hiện", feedback);
        Assert.Contains("Đọc ảnh chân dung: chưa thực hiện", feedback);
    }
}
