CREATE TABLE IF NOT EXISTS activity_logs (
    activity_log_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    module_code TEXT NOT NULL,
    action_code TEXT NOT NULL,
    entity_type TEXT NOT NULL,
    entity_id UUID NULL,
    summary TEXT NULL,
    old_data JSONB NULL,
    new_data JSONB NULL,
    metadata JSONB NULL,
    customer_id UUID NULL,
    loan_contract_id UUID NULL,
    store_id UUID NULL,
    changed_by UUID NULL,
    changed_at_utc TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    ip_address INET NULL,
    request_path TEXT NULL,
    CONSTRAINT fk_activity_logs_customer FOREIGN KEY (customer_id) REFERENCES customers(customer_id) ON DELETE SET NULL,
    CONSTRAINT fk_activity_logs_loan_contract FOREIGN KEY (loan_contract_id) REFERENCES loan_contracts(loan_contract_id) ON DELETE SET NULL,
    CONSTRAINT fk_activity_logs_store FOREIGN KEY (store_id) REFERENCES stores(store_id) ON DELETE SET NULL,
    CONSTRAINT fk_activity_logs_changed_by FOREIGN KEY (changed_by) REFERENCES app_users(user_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_activity_logs_changed_at_utc
    ON activity_logs (changed_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_activity_logs_module_changed_at_utc
    ON activity_logs (module_code, changed_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_activity_logs_action_changed_at_utc
    ON activity_logs (action_code, changed_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_activity_logs_changed_by_changed_at_utc
    ON activity_logs (changed_by, changed_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_activity_logs_customer_id
    ON activity_logs (customer_id);

CREATE INDEX IF NOT EXISTS ix_activity_logs_loan_contract_id
    ON activity_logs (loan_contract_id);

CREATE INDEX IF NOT EXISTS ix_activity_logs_store_id
    ON activity_logs (store_id);
