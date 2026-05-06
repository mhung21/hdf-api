ALTER TABLE "loan_contracts" DROP COLUMN IF EXISTS "disbursement_method";
ALTER TABLE "loan_contracts" DROP COLUMN IF EXISTS "disbursement_bank_name";
ALTER TABLE "loan_contracts" DROP COLUMN IF EXISTS "disbursement_bank_account";

ALTER TABLE "cash_vouchers" ADD COLUMN IF NOT EXISTS "payment_method" text NOT NULL DEFAULT 'CASH';
ALTER TABLE "cash_vouchers" ADD COLUMN IF NOT EXISTS "bank_name" text NULL;
ALTER TABLE "cash_vouchers" ADD COLUMN IF NOT EXISTS "bank_account_number" text NULL;
