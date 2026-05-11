CREATE TABLE contract_audit_logs (
    audit_log_id uuid DEFAULT gen_random_uuid() NOT NULL,
    loan_contract_id uuid NOT NULL,
    action_code character varying NOT NULL,
    old_data jsonb,
    new_data jsonb,
    changed_by uuid,
    changed_at timestamp without time zone DEFAULT now() NOT NULL,
    note character varying
);

ALTER TABLE ONLY contract_audit_logs
    ADD CONSTRAINT contract_audit_logs_pkey PRIMARY KEY (audit_log_id);

ALTER TABLE ONLY contract_audit_logs
    ADD CONSTRAINT contract_audit_logs_changed_by_fkey FOREIGN KEY (changed_by) REFERENCES app_users(user_id) ON DELETE RESTRICT;

ALTER TABLE ONLY contract_audit_logs
    ADD CONSTRAINT contract_audit_logs_loan_contract_id_fkey FOREIGN KEY (loan_contract_id) REFERENCES loan_contracts(loan_contract_id) ON DELETE RESTRICT;

CREATE INDEX ix_contract_audit_logs_changed_at ON contract_audit_logs USING btree (changed_at);
CREATE INDEX ix_contract_audit_logs_loan_contract_id ON contract_audit_logs USING btree (loan_contract_id);
