using System.Collections.Immutable;
using Emrtd;

namespace Icao9303.Core;

public sealed record Dg2Image(string MimeType, ImmutableArray<byte> Bytes);

/// <summary>
/// ICAO APDU sequence. Protocol selection and UI fallbacks belong to the caller;
/// a failed authentication cannot be retried on this session.
/// </summary>
public sealed class IcaoChipSession
{
    private readonly EmrtdSession _session;
    private bool _authenticationAttempted;
    private bool _secure;

    public IcaoChipSession(ICardTransport transport)
    {
        _session = new EmrtdSession(transport);
    }

    public byte[] ReadCardAccess()
    {
        EnsureBeforeAuthentication();
        var selected = _session.SelectFile(Lds.CardAccess);
        return selected.IsSuccess
            ? _session.ReadFile(Lds.CardAccess)
            : _session.ReadFileBySfi(0x1C);
    }

    public void AuthenticatePace(Td1Mrz mrz, PaceInfo pace)
    {
        EnsureBeforeAuthentication();
        if (!pace.IsSupported || pace.Mapping != PaceMapping.GenericMapping)
            throw new NotSupportedException($"Biến thể {pace} không được thư viện hỗ trợ.");
        var key = AccessKey(mrz);
        _authenticationAttempted = true;
        _session.StartSecureMessaging(Pace.Authenticate(_session, key, pace));
        _session.SelectApplet();
        _secure = true;
    }

    public void AuthenticateBac(Td1Mrz mrz)
    {
        EnsureBeforeAuthentication();
        var key = AccessKey(mrz);
        _authenticationAttempted = true;
        _session.SelectApplet();
        _session.StartSecureMessaging(Bac.Authenticate(_session, key));
        _secure = true;
    }

    public ImmutableArray<byte> ReadDg1Raw()
    {
        EnsureSecure();
        return ImmutableArray.CreateRange(_session.ReadFile(Lds.DataGroupFileId(1)));
    }

    public ImmutableArray<byte>? ReadDg2Raw()
    {
        EnsureSecure();
        var raw = _session.TryReadFile(Lds.DataGroupFileId(2));
        return raw is null ? null : ImmutableArray.CreateRange(raw);
    }

    public static ImmutableArray<string> DecodeDg1(ImmutableArray<byte> raw) =>
        ImmutableArray.CreateRange(Lds.ParseDg1(raw.ToArray()));

    public static Dg2Image? DecodeDg2(ImmutableArray<byte> raw)
    {
        var face = Lds.ParseDg2(raw.ToArray());
        return face is null ? null : new Dg2Image(face.MimeType,
            ImmutableArray.CreateRange(face.Data));
    }

    private static MrzKey AccessKey(Td1Mrz mrz)
    {
        var key = new MrzKey(mrz.DocumentNumber, mrz.BirthDate, mrz.ExpiryDate);
        if (key.MrzInformation != mrz.MrzInformation)
            throw new FormatException("Emrtd.MrzKey khác dữ liệu MRZ TD1 đã xác nhận.");
        return key;
    }

    private void EnsureBeforeAuthentication()
    {
        if (_authenticationAttempted)
            throw new InvalidOperationException("Phiên chip đã thử xác thực; cần áp thẻ lại.");
    }

    private void EnsureSecure()
    {
        if (!_secure)
            throw new InvalidOperationException("Chưa có phiên PACE/BAC được bảo vệ.");
    }
}
