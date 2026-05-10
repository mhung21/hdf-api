-- Constraint cũ: ADMIN/REGIONAL_MANAGER bắt buộc store_id IS NULL
-- Sửa lại: cho phép mọi role đều có store_id (NOT NULL)

-- 1. Bỏ constraint cũ
ALTER TABLE app_users DROP CONSTRAINT IF EXISTS ck_app_users_store_scope;

-- 2. Gán chi nhánh "Tổng công ty" cho tất cả ADMIN chưa có store_id
UPDATE app_users
SET    store_id   = (SELECT store_id FROM stores WHERE store_name = 'Tổng công ty' LIMIT 1),
       updated_at = now()
WHERE  role_code  = 'ADMIN'
  AND  store_id IS NULL;

-- 3. Tạo lại constraint: mọi user đều phải có store_id
ALTER TABLE app_users ADD CONSTRAINT ck_app_users_store_scope
  CHECK (store_id IS NOT NULL);
