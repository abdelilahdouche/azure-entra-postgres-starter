CREATE TABLE reports (
    id           uuid PRIMARY KEY,
    store_code   text        NOT NULL,
    title        text        NOT NULL,
    file_name    text        NOT NULL,
    content_type text        NOT NULL,
    blob_name    text        NOT NULL,
    uploaded_by  text        NOT NULL,
    uploaded_at  timestamptz NOT NULL,
    status       text        NOT NULL DEFAULT 'Submitted',
    approved_by  text,
    approved_at  timestamptz,
    CONSTRAINT reports_status_chk CHECK (status IN ('Submitted', 'Approved'))
);

CREATE INDEX reports_status_uploaded_at_idx ON reports (status, uploaded_at DESC);
