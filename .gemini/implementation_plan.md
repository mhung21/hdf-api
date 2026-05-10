# Kế hoạch Thực hiện: Phân bổ ưu tiên và Tính lại lịch trả nợ khi đóng thiếu

Yêu cầu:
1. Khi thu tiền nhưng khách trả không đủ, ưu tiên thu: Lãi -> Phí phần mềm -> Các loại phí khác -> Gốc.
2. Số tiền gốc chưa thu được của kỳ này sẽ cộng vào tiền gốc còn lại.
3. Tính lại công thức PMT cho các kỳ còn lại (thời gian còn lại) dựa trên dư nợ gốc mới.

## User Feedback & Decisions

> **1. Thời điểm tính lại lịch (Recalculate):** 
> *Quyết định:* Chỉ tính lại PMT theo dư nợ gốc mới khi **đến kỳ thanh toán tiếp theo**. Nếu khách hàng trả nốt phần gốc còn thiếu **trước** ngày thanh toán tiếp theo thì hệ thống cứ thu bình thường (gạch nợ vào phần còn thiếu của kỳ hiện tại) mà không cần tính lại lịch.
> 
> **2. Công thức PMT & Xử lý phần thiếu:**
> *Quyết định:* Tiền phạt chậm nộp và các khoản phí thiếu sẽ không cộng vào gốc. Chỉ lấy **Dư nợ gốc thực tế còn lại** tại thời điểm đến kỳ tiếp theo để chạy lại công thức PMT cho số kỳ còn lại.

## Proposed Changes

### 1. Frontend (`hdf-fe/src/app/pages/loans/loan-detail-dialog.component.ts`)

#### [MODIFY] `loan-detail-dialog.component.ts`
- Tách mục thu `PERIODIC_FEE` (Phí định kỳ) trên giao diện Thêm phiếu thu thành 2 mục riêng biệt: `QLKV_FEE` (Phí phần mềm) và `QLTS_FEE` (Phí hao mòn) để người dùng thấy rõ và có thể phân bổ ưu tiên.

### 2. Backend API (`CrediFlow.API/Services/CashVoucherService.cs`)

#### [MODIFY] `CashVoucherService.cs`
- **Cập nhật thứ tự ưu tiên phân bổ:** Sửa hàm `CollectLoanPayment` thành 5 bước ưu tiên: `INTEREST` (Lãi) -> `QLKV_FEE` (Phí phần mềm) -> `QLTS_FEE` (Phí hao mòn) -> `LATE_PENALTY` (Phạt) -> `PRINCIPAL` (Gốc).
- Cập nhật hàm `TryBuildCollectLoanPaymentModel` để nhận diện `QLKV_FEE` và `QLTS_FEE`.

### 3. Backend API (`CrediFlow.API/Services/LoanContractService.cs` & Background Job)

#### [NEW] Logic tính lại lịch (Recalculate Schedule) khi đến hạn kỳ tiếp theo
- Do yêu cầu *"đến kỳ tiếp theo mới tính lại PMT, còn đóng trước kỳ tiếp theo thì thu bình thường"*, việc tính lại lịch sẽ **KHÔNG** chạy ngay lúc tạo Phiếu thu (CashVoucher).
- Thay vào đó, hệ thống cần một cơ chế kiểm tra (khi khách hàng mở popup thanh toán cho kỳ tiếp theo, hoặc thông qua một Cron Job chạy đầu ngày).
- Nếu phát hiện kỳ thanh toán hiện tại đã đến hạn (`DueDate <= Today`), và kỳ trước đó bị thiếu Gốc (`DuePrincipalAmount > PaidPrincipalAmount`), hệ thống sẽ:
  1. Chốt dư nợ gốc thực tế hiện tại.
  2. Xóa các dòng lịch trả nợ từ kỳ hiện tại trở đi.
  3. Tính lại PMT mới cho số kỳ còn lại dựa trên dư nợ gốc thực tế và sinh lại lịch mới vào database.

### Backend API (`CrediFlow.API/Services/LoanContractService.cs`)

#### [MODIFY] `LoanContractService.cs` (hoặc Service tương đương chứa logic sinh lịch)
- Tách hàm tính toán và sinh `LoanRepaymentSchedule` thành một hàm dùng chung.
- Hàm này sẽ dùng công thức PMT chuẩn để tạo ra các dòng schedule mới, đảm bảo có thể được tái sử dụng bởi `CashVoucherService`.

## Verification Plan

### Automated Tests
- Tạo phiếu thu với số tiền nhỏ hơn tổng cần đóng.
- Kiểm tra Database xem các kỳ trả nợ tương lai có được tạo lại với số tiền `DueInterestAmount` và `DuePrincipalAmount` thay đổi dựa trên PMT mới hay không.

### Manual Verification
- Thực hiện thu tiền trên UI màn hình "Chi tiết hợp đồng".
- Chọn một kỳ và nhập số tiền nhỏ hơn quy định.
- Lưu phiếu thu và mở lại tab "Lịch trả nợ" để xem các kỳ tiếp theo có cập nhật số tiền phải đóng mới không.
