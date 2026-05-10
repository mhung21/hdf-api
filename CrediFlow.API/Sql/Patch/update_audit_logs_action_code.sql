-- Bước 1: Xem các action_code đang tồn tại trong bảng (chạy riêng để kiểm tra):
-- SELECT DISTINCT action_code FROM audit_logs ORDER BY action_code;

-- Bước 2: Xem constraint hiện tại (chạy riêng):
-- SELECT conname, pg_get_constraintdef(oid)
-- FROM pg_constraint
-- WHERE conname = 'ck_audit_logs_action_code';

-- Bước 3: Bỏ constraint cũ và tạo lại bao gồm TẤT CẢ giá trị hiện có + 'ASSIGN'
ALTER TABLE audit_logs DROP CONSTRAINT IF EXISTS ck_audit_logs_action_code;

ALTER TABLE audit_logs ADD CONSTRAINT ck_audit_logs_action_code
  CHECK (action_code = ANY (ARRAY[
    'CREATE'::text,
	'INSERT'::text,
    'UPDATE'::text,
    'DELETE'::text,
    'ASSIGN'::text,
    'STATUS_CHANGE'::text,
    'LOGIN'::text,
    'LOGOUT'::text,
    'APPROVE'::text,
    'REJECT'::text,
    'DISBURSE'::text,
    'CANCEL'::text,
    'SETTLE'::text,
    'COLLECT'::text,
    'TRANSFER'::text,
    'IMPORT'::text,
    'EXPORT'::text
  ]));
