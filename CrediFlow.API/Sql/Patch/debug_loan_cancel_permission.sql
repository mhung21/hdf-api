-- ====================================================================
-- FIX: Xóa user_permissions override thừa cho LOAN_CANCEL
-- ====================================================================

-- 1. Kiểm tra: Bao nhiêu user có override LOAN_CANCEL?
SELECT u.user_name, u.role_code, up.is_granted, p.permission_code
FROM user_permissions up
JOIN permissions p ON up.permission_id = p.permission_id
JOIN app_users u ON up.user_id = u.user_id
WHERE p.permission_code = 'LOAN_CANCEL';

-- 2. Kiểm tra TOÀN BỘ user_permissions: có bao nhiêu override?
--    Nếu số lượng lớn → có thể bị bulk-insert tất cả permissions cho mọi user
SELECT u.user_name, u.role_code, COUNT(*) AS override_count
FROM user_permissions up
JOIN app_users u ON up.user_id = u.user_id
GROUP BY u.user_name, u.role_code
ORDER BY override_count DESC;

-- 3. FIX: Xóa TẤT CẢ user_permissions (override per-user) vì đây là dữ liệu thừa
--    Hệ thống nên dùng role_permissions + custom_role_permissions là đủ
-- ⚠️ UNCOMMENT dòng dưới để chạy sau khi xác nhận
-- DELETE FROM user_permissions;
