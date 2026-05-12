-- ============================================================================
-- RESTORE: Khôi phục dữ liệu lịch trả nợ từ bảng backup
-- Chạy script này NẾU RecalculateAllSchedules cho kết quả SAI
-- ============================================================================

-- 1. Kiểm tra backup tồn tại
SELECT COUNT(*) AS backup_rows FROM _backup_loan_repayment_schedules_20260512;

-- 2. Restore: UPDATE lại toàn bộ loan_repayment_schedules từ backup
-- (Chỉ cập nhật các cột tài chính, giữ nguyên ID)
UPDATE loan_repayment_schedules s
SET
    period_from_date         = b.period_from_date,
    period_to_date           = b.period_to_date,
    due_date                 = b.due_date,
    actual_day_count         = b.actual_day_count,
    opening_principal_amount = b.opening_principal_amount,
    installment_amount       = b.installment_amount,
    due_principal_amount     = b.due_principal_amount,
    due_interest_amount      = b.due_interest_amount,
    due_qlkv_amount          = b.due_qlkv_amount,
    due_qlts_amount          = b.due_qlts_amount,
    due_periodic_fee_amount  = b.due_periodic_fee_amount,
    due_late_penalty_amount  = b.due_late_penalty_amount,
    paid_principal_amount    = b.paid_principal_amount,
    paid_interest_amount     = b.paid_interest_amount,
    paid_qlkv_amount         = b.paid_qlkv_amount,
    paid_qlts_amount         = b.paid_qlts_amount,
    paid_periodic_fee_amount = b.paid_periodic_fee_amount,
    paid_late_penalty_amount = b.paid_late_penalty_amount,
    closing_principal_amount = b.closing_principal_amount,
    status_code              = b.status_code,
    fully_paid_at            = b.fully_paid_at,
    note                     = b.note,
    updated_at               = b.updated_at
FROM _backup_loan_repayment_schedules_20260512 b
WHERE s.schedule_id = b.schedule_id;

-- 3. Kiểm tra: các bản ghi mới thêm bởi Recalculate (không có trong backup)
-- Nếu có, cần xóa thủ công hoặc đánh dấu CANCELLED
SELECT s.schedule_id, s.loan_contract_id, s.period_no, s.status_code
FROM loan_repayment_schedules s
LEFT JOIN _backup_loan_repayment_schedules_20260512 b ON b.schedule_id = s.schedule_id
WHERE b.schedule_id IS NULL
ORDER BY s.loan_contract_id, s.period_no;

-- 4. Đánh dấu CANCELLED các bản ghi mới thêm (nếu có)
-- Bỏ comment dòng dưới nếu muốn chạy:
/*
UPDATE loan_repayment_schedules s
SET status_code = 'CANCELLED',
    opening_principal_amount = 0,
    installment_amount = 0,
    due_principal_amount = 0,
    due_interest_amount = 0,
    due_qlkv_amount = 0,
    due_qlts_amount = 0,
    due_periodic_fee_amount = 0,
    closing_principal_amount = 0,
    updated_at = NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM _backup_loan_repayment_schedules_20260512 b
    WHERE b.schedule_id = s.schedule_id
);
*/

-- 5. Xác nhận sau khi restore
SELECT 
    'Restored rows' AS status,
    COUNT(*) AS total
FROM loan_repayment_schedules s
JOIN _backup_loan_repayment_schedules_20260512 b ON b.schedule_id = s.schedule_id
WHERE s.updated_at = b.updated_at;

-- ============================================================================
-- SAU KHI XÁC NHẬN THÀNH CÔNG, có thể xóa bảng backup:
-- DROP TABLE IF EXISTS _backup_loan_repayment_schedules_20260512;
-- ============================================================================
