# Đầu vào khóa và phiên chip

## Vai trò

Giải thích đường dữ liệu trong [`Td1Mrz`](../Icao9303.Core/Td1Mrz.cs), [`IcaoChipSession`](../Icao9303.Core/IcaoChipSession.cs) và lớp Android. Quy trình UI và giao tiếp đầy đủ nằm trong [contract chung](quy-trinh-va-contract.md).

## Quy trình

### 1. Lấy đúng trường TD1

Vị trí trong bảng đếm từ 1, bao gồm cả hai đầu. Mỗi dòng có 30 ký tự.

| Dữ liệu | Vị trí |
| --- | --- |
| Số giấy tờ cho khóa | Dòng 1, 6–14: chín ký tự, kể cả `<` đệm |
| Checksum số giấy tờ | Dòng 1, 15 |
| Ngày sinh | Dòng 2, 1–6: YYMMDD |
| Checksum ngày sinh | Dòng 2, 7 |
| Ngày hết hạn | Dòng 2, 9–14: YYMMDD |
| Checksum hết hạn | Dòng 2, 15 |
| Checksum tổng hợp | Dòng 2, 30; dùng để kiểm tra toàn bộ phần tương ứng, không ghép vào đầu vào khóa |

Vùng tùy chọn không được tự thay thế trường chín ký tự. Checksum dùng giá trị `0–9 → 0–9`, `A–Z → 10–35`, `< → 0`; trọng số lặp `7, 3, 1`, tổng lấy modulo 10. Họ tên không nằm trong checksum tổng hợp nên vẫn cần đối chiếu và so sánh đủ ba dòng khi đọc chip.

### 2. Tạo đầu vào truy cập

```text
MRZ_information = documentNumberField(9) + documentCheckDigit(1)
                + birthDate(6) + birthDateCheckDigit(1)
                + expiryDate(6) + expiryDateCheckDigit(1)
H = SHA-1(ASCII(MRZ_information))
```

Mẫu ICAO trong test tạo `D23145890774081221204159`, hash `3C4E2EDB7BE894F54FA2CC9A04EF09D0887526A3`. Đây là mẫu kiểm thử, không phải dữ liệu người dùng. `IcaoChipSession.AccessKey` tạo `Emrtd.MrzKey` và yêu cầu chuỗi thư viện tạo phải trùng nguyên văn `Td1Mrz.MrzInformation`, gồm ảnh hưởng của ký tự đệm.

`MRZ_information` và H không phải khóa phiên; chúng không được hiện trên màn hình chính. Không gửi trực tiếp khóa bí mật cho chip.

### 3. Chọn và thực hiện giao thức

Đọc EF.CardAccess trước xác thực. Lõi thử chọn file; chọn không thành công thì đọc qua SFI `1C`. Android phân tích danh sách PACE và chọn Generic Mapping được thư viện hỗ trợ. CardAccess vắng/bị từ chối hoặc không có biến thể phù hợp dẫn tới đường BAC.

Với PACE: kiểm tra biến thể/đầu vào → `Pace.Authenticate` → bật secure messaging → chọn applet eMRTD. Với BAC: kiểm tra đầu vào → chọn applet eMRTD → `Bac.Authenticate` → bật secure messaging. KDF, trao đổi mật mã, khóa phiên, MAC và bộ đếm do `Emrtd` sở hữu; app không thay bằng phép băm tự viết.

### 4. Đọc và phân tích dữ liệu

Sau xác thực, DG1 (`0x0101`) được đọc qua secure messaging rồi phân tích thành các dòng MRZ. `ChipMrzResult` kiểm tra TD1 và so sánh với bản đã xác nhận. DG2 (`0x0102`) được thử tiếp để lấy ảnh; ảnh JPEG mới được giải mã hiển thị. Byte mà app nhận từ `ReadDg1Raw/ReadDg2Raw` đã là nội dung LDS sau khi thư viện xử lý secure messaging.

## Contract

Lõi không nhận ảnh/OCR thô. Caller cung cấp `Td1Mrz` đã kiểm tra và `ICardTransport`. Mỗi APDU được transport chuyển đúng một lần; không retry lệnh bảo vệ hoặc tự gửi plain GET RESPONSE. Chỉ `_secure == true` mới cho đọc DG. Một lần xác thực đã bắt đầu, dù thất bại, không được thử lại trên cùng `IcaoChipSession`.

Phản hồi transport chứa SW ngoài ở hai byte cuối. Với secure messaging, trạng thái và dữ liệu bên trong được thư viện xác minh/giải gói. UI trình bày dữ liệu đã phân tích, không trình bày APDU mã hóa như thông tin giấy tờ.

## Lỗi và giới hạn

PACE-CAM và biến thể không được hỗ trợ bị từ chối trước xác thực. PACE đã bắt đầu rồi lỗi thì phải nhấc thẻ, không chuyển BAC cùng phiên. Lỗi MAC, APDU bị từ chối, mất thẻ và MRZ sai là các khả năng riêng; không suy ra nguyên nhân duy nhất từ lỗi xác thực.

Không có xác minh SOD/chuỗi CSCA, EAC cho DG3/DG4 hoặc hiển thị JPEG 2000 trong luồng hiện tại. Bắt tay thành công và đọc DG không phải kết luận giấy tờ thật. Chuẩn tham chiếu là ICAO Doc 9303 về TD1, LDS và giao thức truy cập; tập hỗ trợ thực tế do mã và phiên bản thư viện quyết định.

## Kiểm chứng

Mẫu TD1 kiểm tra được chuỗi/hash và các điều kiện trạng thái; transport giả kiểm tra không đọc DG trước xác thực và không xác thực lại. Kiểm thử này không thay một chip thật. Xem [kiểm chứng](kiem-thu.md).
