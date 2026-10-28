using System.Security.Cryptography;
using System.Text;

namespace Icao9303.Core;

/// <summary>A check digit read from the document and the digit calculated by ICAO Doc 9303.</summary>
public sealed record Td1CheckDigit(char Read, char Expected)
{
    public bool IsValid => Read == Expected;
}

/// <summary>Exact TD1 lines and their check results. No OCR correction happens here.</summary>
public sealed record Td1Inspection(
    string First,
    string Second,
    string Third,
    Td1CheckDigit DocumentNumber,
    Td1CheckDigit BirthDate,
    Td1CheckDigit ExpiryDate,
    Td1CheckDigit Composite)
{
    public bool AllChecksPass => DocumentNumber.IsValid && BirthDate.IsValid &&
        ExpiryDate.IsValid && Composite.IsValid;
}

/// <summary>
/// Validated TD1 input for MRZ-based BAC/PACE. The supported subset has a document number
/// contained in its nine-character field and numeric YYMMDD dates.
/// </summary>
public sealed class Td1Mrz
{
    private Td1Mrz(Td1Inspection inspection)
    {
        Inspection = inspection;
    }

    public Td1Inspection Inspection { get; }
    public string First => Inspection.First;
    public string Second => Inspection.Second;
    public string Third => Inspection.Third;
    public string DocumentNumberField => First.Substring(5, 9);
    public string DocumentNumber => DocumentNumberField.TrimEnd('<');
    public string BirthDate => Second[..6];
    public string ExpiryDate => Second.Substring(8, 6);
    public string MrzInformation => First.Substring(5, 10) + Second[..7] + Second.Substring(8, 7);
    public byte[] MrzHash() => SHA1.HashData(Encoding.ASCII.GetBytes(MrzInformation));

    public static Td1Inspection Inspect(string first, string second, string third)
    {
        ValidateLine(first, 1);
        ValidateLine(second, 2);
        ValidateLine(third, 3);

        return new Td1Inspection(
            first, second, third,
            new Td1CheckDigit(first[14], CheckDigit(first.Substring(5, 9))),
            new Td1CheckDigit(second[6], CheckDigit(second[..6])),
            new Td1CheckDigit(second[14], CheckDigit(second.Substring(8, 6))),
            new Td1CheckDigit(second[29], CheckDigit(CompositeData(first, second))));
    }

    public static Td1Mrz ParseValidated(string first, string second, string third)
    {
        var inspection = Inspect(first, second, third);
        if (first[14] == '<')
            throw new NotSupportedException("Số giấy tờ TD1 kéo dài sang vùng tùy chọn chưa được hỗ trợ.");
        if (!inspection.AllChecksPass)
            throw new FormatException("Một hoặc nhiều chữ số kiểm tra TD1 không khớp.");
        if (!second[..6].All(char.IsAsciiDigit) || !second.Substring(8, 6).All(char.IsAsciiDigit))
            throw new NotSupportedException("Ngày sinh/ngày hết hạn không đủ 6 chữ số để tạo khóa MRZ.");

        if (first.Substring(5, 9).TrimEnd('<').Length == 0)
            throw new FormatException("Trường số giấy tờ TD1 trống.");
        return new Td1Mrz(inspection);
    }

    public static char CheckDigit(string value)
    {
        var sum = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var number = value[i] switch
            {
                >= '0' and <= '9' => value[i] - '0',
                >= 'A' and <= 'Z' => value[i] - 'A' + 10,
                '<' => 0,
                _ => throw new FormatException("Ký tự ngoài bảng chữ MRZ.")
            };
            sum += number * ((i % 3) switch { 0 => 7, 1 => 3, _ => 1 });
        }
        return (char)('0' + sum % 10);
    }

    private static string CompositeData(string first, string second) =>
        first.Substring(5, 25) + second[..7] +
        second.Substring(8, 7) + second.Substring(18, 11);

    private static void ValidateLine(string line, int number)
    {
        if (line is null || line.Length != 30 ||
            line.Any(c => c is not (>= 'A' and <= 'Z' or >= '0' and <= '9' or '<')))
            throw new FormatException($"Dòng TD1 {number} phải có đúng 30 ký tự A-Z, 0-9 hoặc <.");
    }
}
