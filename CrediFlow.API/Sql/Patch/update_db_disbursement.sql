ALTER TABLE "cash_vouchers" ADD COLUMN IF NOT EXISTS "payment_method" text NULL;
ALTER TABLE "cash_vouchers" ADD COLUMN IF NOT EXISTS "bank_name" text NULL;
ALTER TABLE "cash_vouchers" ADD COLUMN IF NOT EXISTS "bank_account_number" text NULL;
