-- Thêm quyền mới vào bảng permissions
INSERT INTO permissions (
    permission_id, 
    permission_code, 
    permission_name, 
    resource, 
    action, 
    description, 
    is_active, 
    is_delegatable
)
VALUES (
    gen_random_uuid(), 
    'REPORT_ALL_EXPORT', 
    'Xuất Excel Toàn Hệ Thống', 
    'ReportAll', 
    'ExportExcel', 
    'Cho phép xuất dữ liệu báo cáo toàn hệ thống ra file Excel', 
    true, 
    true
)
ON CONFLICT (permission_code) DO NOTHING;

-- Tự động gán quyền này cho vai trò ADMIN
INSERT INTO role_permissions (
    role_permission_id, 
    role_code, 
    permission_id, 
    created_at
)
SELECT 
    gen_random_uuid(), 
    'ADMIN', 
    p.permission_id, 
    NOW()
FROM permissions p
WHERE p.permission_code = 'REPORT_ALL_EXPORT'
AND NOT EXISTS (
    SELECT 1 FROM role_permissions rp 
    WHERE rp.role_code = 'ADMIN' AND rp.permission_id = p.permission_id
);
