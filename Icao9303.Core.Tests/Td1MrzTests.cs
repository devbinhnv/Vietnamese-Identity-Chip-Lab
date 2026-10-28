using Icao9303.Core;
using VietnameseIdentityChipLab;
using Emrtd;
using Xunit;

namespace Icao9303.Core.Tests;

public sealed class Td1MrzTests
{
    private const string First = "I<UTOD231458907<<<<<<<<<<<<<<<";
    private const string Second = "7408122F1204159UTO<<<<<<<<<<<6";
    private const string Third = "ERIKSSON<<ANNA<MARIA<<<<<<<<<<";

    [Fact]
    public void IcaoTd1Specimen_ProducesExactAccessInputAndHash()
    {
        var mrz = Td1Mrz.ParseValidated(First, Second, Third);

        Assert.Equal("D23145890", mrz.DocumentNumber);
        Assert.Equal("D23145890774081221204159", mrz.MrzInformation);
        Assert.Equal("3C4E2EDB7BE894F54FA2CC9A04EF09D0887526A3",
            Convert.ToHexString(mrz.MrzHash()));
        Assert.True(mrz.Inspection.AllChecksPass);
    }

    [Fact]
    public void OcrLetterO_IsReportedButNeverCorrectedByStandardParser()
    {
        var mistaken = Second[..6] + 'O' + Second[7..];

        var inspection = Td1Mrz.Inspect(First, mistaken, Third);
        Assert.Equal('O', inspection.BirthDate.Read);
        Assert.Equal('2', inspection.BirthDate.Expected);
        Assert.False(inspection.AllChecksPass);
        Assert.Throws<FormatException>(() => Td1Mrz.ParseValidated(First, mistaken, Third));
        Assert.False(MrzAccess.TryPrepare(string.Join('\n', First, mistaken, Third), out _, out _));
    }

    [Fact]
    public void InputAdapter_OnlyNormalizesLineEndings()
    {
        Assert.True(MrzAccess.TryPrepare($"{First}\r\n{Second}\r\n{Third}\r\n", out var mrz, out _));
        Assert.Equal(First, mrz!.First);
        Assert.False(MrzAccess.TryPrepare($"{First} \n{Second}\n{Third}", out _, out _));
        Assert.False(MrzAccess.TryPrepare($"{First}\n\n{Second}\n{Third}", out _, out _));
    }

    [Fact]
    public void LiveOcrCandidate_UsesOnlyConsecutiveExactLines()
    {
        var raw = $"Camera text\n{First}\n{Second}\n{Third}\nOther text";
        Assert.True(LiveMrzCandidate.TryFindValidatedTd1(raw, out var candidate));
        Assert.Equal(string.Join('\n', First, Second, Third), candidate);
        Assert.False(LiveMrzCandidate.TryFindValidatedTd1(
            $"{First}\nnoise\n{Second}\n{Third}", out _));
        Assert.False(LiveMrzCandidate.TryFindValidatedTd1(
            $"{First}\n{Second[..6]}O{Second[7..]}\n{Third}", out _));
    }

    [Fact]
    public void LiveOcrCandidate_RemovesOcrWhitespaceButRejectsMissingMrzCharacters()
    {
        var spaced = $"{First[..10]} {First[10..]}\n{Second[..15]}\t{Second[15..]}\n{Third}";
        Assert.True(LiveMrzCandidate.TryFindValidatedTd1(spaced, out var candidate));
        Assert.Equal(string.Join('\n', First, Second, Third), candidate);
        Assert.False(LiveMrzCandidate.TryFindValidatedTd1(
            $"{First[..10]}{First[11..]}\n{Second}\n{Third}", out _));
        Assert.False(LiveMrzCandidate.TryFindValidatedTd1(
            $"{First}\n{Second[..29]}0\n{Third}", out _));
    }

    [Fact]
    public void AuthenticationCannotBeRetriedWithinOneChipSession()
    {
        var mrz = Td1Mrz.ParseValidated(First, Second, Third);
        using var transport = new RejectingTransport();
        var session = new IcaoChipSession(transport);

        Assert.Throws<InvalidOperationException>(() => session.ReadDg1Raw());
        Assert.Throws<Emrtd.CardException>(() => session.AuthenticateBac(mrz));
        Assert.Throws<InvalidOperationException>(() => session.AuthenticateBac(mrz));
    }

    [Fact]
    public void PaddedDocumentNumber_UsesExactTd1FieldForAccessInput()
    {
        var first = First[..5] + "D2314589<" + Td1Mrz.CheckDigit("D2314589<") + First[15..];
        var second = Second[..29] + Td1Mrz.CheckDigit(first.Substring(5, 25) +
            Second[..7] + Second.Substring(8, 7) + Second.Substring(18, 11));

        var mrz = Td1Mrz.ParseValidated(first, second, Third);

        Assert.Equal("D2314589<", mrz.DocumentNumberField);
        Assert.Equal("D2314589", mrz.DocumentNumber);
        Assert.Equal(mrz.MrzInformation,
            new MrzKey(mrz.DocumentNumber, mrz.BirthDate, mrz.ExpiryDate).MrzInformation);
    }

    [Fact]
    public void UnverifiedPaceCam_IsRejectedBeforeAnyApdu()
    {
        var mrz = Td1Mrz.ParseValidated(First, Second, Third);
        using var transport = new CountingTransport();
        var session = new IcaoChipSession(transport);
        var cam = new PaceInfo("0.4.0.127.0.7.2.2.4.6.2", 2, 12);

        Assert.Equal(PaceMapping.ChipAuthenticationMapping, cam.Mapping);
        Assert.Throws<NotSupportedException>(() => session.AuthenticatePace(mrz, cam));
        Assert.Equal(0, transport.TransmitCount);
        Assert.Throws<CardException>(() => session.AuthenticateBac(mrz));
        Assert.Equal(1, transport.TransmitCount);
    }

    private sealed class RejectingTransport : Emrtd.ICardTransport
    {
        public byte[] Transmit(byte[] _) => [0x6A, 0x82];
        public void Reset() => throw new NotSupportedException();
        public void Dispose() { }
    }

    private sealed class CountingTransport : ICardTransport
    {
        public int TransmitCount { get; private set; }
        public byte[] Transmit(byte[] _) { TransmitCount++; return [0x6A, 0x82]; }
        public void Reset() => throw new NotSupportedException();
        public void Dispose() { }
    }
}
