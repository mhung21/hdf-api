# Hướng dẫn Công thức Tính Toán Khoản Vay

Tài liệu này mô tả chi tiết công thức tính toán lãi suất, phí và lịch trả nợ cho khách hàng được áp dụng trong hệ thống HDF. Hệ thống hỗ trợ 2 loại hợp đồng chính: **Cầm đồ** và **Cầm cố / Thuê lại (Trả góp)**.

> [!NOTE]
> **Bảo hiểm khoản vay:** Nếu khách hàng có mua bảo hiểm khoản vay, số tiền bảo hiểm sẽ được **cộng trực tiếp vào Số tiền vay gốc** để tính lãi và phí cho cả 2 loại hợp đồng.

---

## 1. Hợp đồng Cầm đồ (PAWN)

Đặc điểm của hợp đồng cầm đồ là khách hàng chỉ đóng tiền lãi và phí quản lý tài sản (QLTS) theo từng chu kỳ ngắn hạn (thường tính bằng ngày). Tiền gốc sẽ được thanh toán vào kỳ cuối cùng khi khách hàng chuộc lại tài sản.

### 1.1. Các thông số đầu vào
- **Gốc tính toán (`G`)**: Số tiền cầm đồ + Tiền bảo hiểm (nếu có).
- **Số ngày của 1 kỳ (`D`)**: Số ngày quy định cho mỗi lần đóng tiền (ví dụ: 10 ngày, 15 ngày, 30 ngày).
- **Số kỳ (`N`)**: Tổng số kỳ cầm đồ.
- **Lãi suất cầm đồ (`L`)**: Tính theo đơn vị **VNĐ / 1 triệu đồng / 1 ngày**.
- **Phí Quản lý tài sản (`P`)**: Tính theo đơn vị **VNĐ / 1 triệu đồng / 1 ngày**.

### 1.2. Công thức tính toán cho mỗi kỳ
Mỗi kỳ, hệ thống sẽ tính tiền lãi và phí dựa trên số ngày thực tế của kỳ đó (mặc định là `D` ngày):

1. **Tiền Lãi hàng kỳ:**
   ```text
   Tiền Lãi = (G * L * D) / 1,000,000
   ```
2. **Phí QLTS hàng kỳ:**
   ```text
   Phí QLTS = (G * P * D) / 1,000,000
   ```
3. **Tổng tiền khách hàng phải đóng hàng kỳ (chưa bao gồm gốc):**
   ```text
   Tổng thanh toán = Tiền Lãi + Phí QLTS
   ```

> [!IMPORTANT]
> - Tiền gốc không giảm dần qua các kỳ. Khách hàng sẽ đóng nguyên số tiền gốc ban đầu khi tiến hành **Tất toán** hợp đồng để nhận lại tài sản.
> - Kết quả của từng phép tính sẽ được làm tròn đến hàng đơn vị (VNĐ).

---

## 2. Hợp đồng Cầm cố / Thuê lại (INSTALLMENT)

Đặc điểm của hợp đồng này là khách hàng sẽ trả góp **đều đặn hàng tháng** (bao gồm cả một phần Tiền gốc + Tiền lãi + Các loại phí). Đây là hình thức trả góp (gốc giảm dần).

### 2.1. Các thông số đầu vào
- **Gốc tính toán (`G`)**: Số tiền vay + Tiền bảo hiểm (nếu có).
- **Kỳ hạn (`N`)**: Số tháng vay.
- **Lãi suất (`R_lai`)**: % / tháng.
- **Phí phần mềm / QLKV (`R_pm`)**: % / tháng.
- **Phí hao mòn / QLTS (`R_hm`)**: % / tháng.
- **Phí định kỳ (`F_dk`)**: Phí thu cố định bằng VNĐ / tháng.

### 2.2. Tính số tiền thanh toán cố định (PMT)
Để đảm bảo mỗi tháng khách hàng đóng một số tiền bằng nhau, hệ thống sử dụng công thức PMT kinh điển dựa trên **Tổng lãi & phí theo tỷ lệ %**:

```text
Tổng tỷ lệ hàng tháng (R_tong) = R_lai + R_pm + R_hm

Số tiền đóng cố định dự kiến (PMT) = [ G * R_tong * (1 + R_tong)^N ] / [ (1 + R_tong)^N - 1 ]
```
*(Nếu `R_tong = 0`, PMT = G / N)*

### 2.3. Quy đổi theo ngày thực tế từng tháng
Vì số ngày trong các tháng là khác nhau (28, 30, 31 ngày), hệ thống sẽ phân bổ lãi và phí dựa trên **số ngày thực tế** của từng tháng, kết hợp với "Hệ số quy đổi" để đảm bảo tổng số tiền vẫn khớp với bảng tính PMT.

- **Ngày đến hạn từng kỳ**: Là ngày tương ứng của tháng tiếp theo tính từ ngày giải ngân.
- **Số ngày của kỳ n (`D_n`)**: Số ngày tính từ ngày đến hạn kỳ trước đến ngày đến hạn kỳ hiện tại.
- **Hệ số quy đổi ngày (`H`)** = `N / Tổng số ngày thực tế của toàn bộ hợp đồng`

### 2.4. Công thức phân bổ chi tiết cho Kỳ `n`

Tại kỳ thứ `n`, gọi `G_con_lai` là dư nợ gốc tính đến đầu kỳ `n` (Với kỳ 1, `G_con_lai = G`):

1. **Tiền Lãi kỳ n:**
   ```text
   Tiền Lãi = G_con_lai * R_lai * H * D_n
   ```

2. **Tổng các loại phí kỳ n:**
   ```text
   Phí phần mềm = (G_con_lai * R_pm * H * D_n) + (F_dk * H * D_n)
   Phí hao mòn = G_con_lai * R_hm * H * D_n
   Tổng Phí = Phí phần mềm + Phí hao mòn
   ```

3. **Tiền Gốc trả kỳ n:**
   Đối với các kỳ từ `1` đến `N-1`:
   ```text
   Gốc trả = PMT - Tiền Lãi - Tổng Phí
   ```
   *(Hệ thống sẽ đảm bảo Gốc trả không vượt quá `G_con_lai` và không bị âm).*

   **Đặc biệt đối với kỳ cuối cùng (Kỳ `N`):**
   ```text
   Gốc trả = Toàn bộ G_con_lai (để đảm bảo dư nợ về 0)
   ```

4. **Tổng thanh toán kỳ n:**
   ```text
   Tổng đóng kỳ n = Gốc trả + Tiền Lãi + Tổng Phí
   ```

5. **Cập nhật Dư nợ gốc cho kỳ sau:**
   ```text
   G_con_lai (mới) = G_con_lai (cũ) - Gốc trả
   ```

> [!TIP]
> Do tính toán dựa trên số ngày thực tế, `Tổng đóng kỳ n` của các tháng có thể **lệch nhau một vài nghìn đồng** (tháng 31 ngày sẽ đóng nhỉnh hơn tháng 30 ngày một chút), nhưng bù lại dư nợ gốc giảm chính xác tuyệt đối theo thời gian khách hàng thực tế sử dụng vốn.

---

## 3. Các loại phí thu một lần (Upfront Fees)
Ngoài lịch trả nợ, hệ thống có lưu ý một số phí khách hàng phải thanh toán hoặc bị khấu trừ ngay tại thời điểm giải ngân:
- **Phí hồ sơ / Phí làm hợp đồng:** Tính theo % trên số tiền vay hoặc nhập số tiền VNĐ cố định. Khách hàng phải thanh toán phí này trước hoặc bị trừ trực tiếp vào tiền giải ngân.
- **Phí Bảo hiểm:** Tính tương tự phí hồ sơ, nhưng thay vì thu ngay, hệ thống cho phép **cộng gộp** số tiền bảo hiểm vào Dư nợ gốc để khách hàng trả dần theo thời gian vay.

---

## 4. Tất toán trước hạn và Hoàn trả lãi/phí đóng dư

Trong trường hợp khách hàng đến tất toán khoản vay sớm hơn so với số ngày đã đóng tiền, hệ thống sẽ tính toán số tiền lãi và phí đóng dư để hoàn trả lại (trừ vào số tiền cần thanh toán của phiếu tất toán).

**Ví dụ thực tế:**
- Khách hàng có ngày đến hạn trả lãi/phí là **8/5**.
- Vào ngày **6/5**, khách hàng đóng đủ tiền lãi và phí của kỳ đó (tức là đã thanh toán tiền lãi/phí đến hết ngày 8/5).
- Tuy nhiên, sang ngày **7/5**, khách hàng đến **Tất toán khoản vay**.
- Lúc này, khoản vay thực tế chỉ kéo dài đến ngày 7/5, nhưng khách hàng đã đóng tiền cho cả ngày 8/5. Khoảng thời gian từ 8/5 đến 8/5 (1 ngày) là thời gian chưa sử dụng vốn nhưng đã trả phí.

**Công thức tính số tiền hoàn trả (lãi/phí trả trước còn thừa):**

1. **Xác định số ngày đóng dư (`D_du`):**
   ```text
   D_du = Ngày đã đóng phí đến (8/5) - Ngày tất toán thực tế (7/5) = 1 ngày
   ```

2. **Tính số tiền Lãi đóng dư (trả lại khách):**
   ```text
   Lãi hoàn trả = (Tiền lãi 1 ngày) * D_du
   ```
   *(Tiền lãi 1 ngày được tính dựa trên Gốc dư nợ hiện tại nhân với Lãi suất quy ngày)*

3. **Tính số tiền Phí đóng dư (Phí QLTS, QLKV...):**
   ```text
   Phí hoàn trả = (Tiền phí 1 ngày) * D_du
   ```

4. **Tổng tiền hoàn trả:**
   ```text
   Số tiền hoàn trả = Lãi hoàn trả + Phí hoàn trả
   ```

Khi lập phiếu tất toán, số tiền khách hàng cần thanh toán cuối cùng sẽ được **trừ đi số tiền hoàn trả** này:
```text
Tổng tiền phải thu khi tất toán = Nợ gốc còn lại + Phí phạt tất toán trước hạn (nếu có) - Số tiền hoàn trả (lãi/phí đóng dư)
```
