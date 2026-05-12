-- ============================================================================
-- BACKUP trước khi chạy RecalculateAllSchedules
-- Chạy script này TRƯỚC KHI gọi API RecalculateAllSchedules
-- Ngày: 2026-05-12
-- ============================================================================

-- 1. Tạo bảng backup lịch trả nợ
DROP TABLE IF EXISTS _backup_loan_repayment_schedules_20260512;

CREATE TABLE _backup_loan_repayment_schedules_20260512 AS
SELECT * FROM loan_repayment_schedules;

-- 2. Đếm số bản ghi đã backup
SELECT 
    'loan_repayment_schedules' AS table_name,
    COUNT(*) AS total_rows
FROM _backup_loan_repayment_schedules_20260512;

-- 3. Kiểm tra: danh sách HĐ sẽ bị ảnh hưởng bởi RecalculateAllSchedules
-- (HĐ trả góp, có lịch, chưa có kỳ nào PAID hoặc đã trả gốc > 0)
SELECT 
    lc.contract_no,
    lc.principal_amount,
    lc.term_months,
    lc.status_code,
    COUNT(s.schedule_id) AS schedule_count
FROM loan_contracts lc
JOIN loan_repayment_schedules s ON s.loan_contract_id = lc.loan_contract_id
WHERE lc.contract_type = 'INSTALLMENT'
  AND NOT EXISTS (
      SELECT 1 FROM loan_repayment_schedules s2
      WHERE s2.loan_contract_id = lc.loan_contract_id
        AND (s2.status_code = 'PAID' OR s2.paid_principal_amount > 0)
  )
GROUP BY lc.loan_contract_id, lc.contract_no, lc.principal_amount, lc.term_months, lc.status_code
ORDER BY lc.contract_no;
