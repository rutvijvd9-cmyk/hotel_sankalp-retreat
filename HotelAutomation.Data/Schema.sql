-- HotelAutomation SQLite Schema
-- All statements use IF NOT EXISTS so running this multiple times is safe.

CREATE TABLE IF NOT EXISTS MessageTemplates (
    Id                   INTEGER PRIMARY KEY AUTOINCREMENT,
    Name                 TEXT    NOT NULL,
    ExternalTemplateName TEXT    NOT NULL,
    LanguageCode         TEXT    NOT NULL DEFAULT 'en',
    ChannelType          TEXT    NOT NULL DEFAULT 'WhatsApp',
    CreatedAt            TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS AutomationRules (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    TriggerEventType TEXT    NOT NULL,
    TemplateId       INTEGER NOT NULL REFERENCES MessageTemplates(Id),
    IsEnabled        INTEGER NOT NULL DEFAULT 1,
    ConditionsJson   TEXT    NULL
);

CREATE TABLE IF NOT EXISTS OutboxMessages (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    ToPhoneNumber  TEXT    NOT NULL,
    TemplateId     INTEGER NOT NULL REFERENCES MessageTemplates(Id),
    ParametersJson TEXT    NOT NULL,
    Status         TEXT    NOT NULL DEFAULT 'Pending',
    RetryCount     INTEGER NOT NULL DEFAULT 0,
    CreatedAt      TEXT    NOT NULL,
    SentAt         TEXT    NULL
);

CREATE TABLE IF NOT EXISTS EventLog (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    EventType       TEXT    NOT NULL,
    RawPayloadJson  TEXT    NULL,
    OutboxMessageId INTEGER NULL REFERENCES OutboxMessages(Id),
    OccurredAt      TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS AppConfig (
    Key   TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
