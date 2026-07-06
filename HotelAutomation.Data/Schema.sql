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
    ConditionsJson   TEXT    NULL,
    DelayMinutes     INTEGER NOT NULL DEFAULT 0
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

CREATE TABLE IF NOT EXISTS ScheduledJobs (
    Id          TEXT    PRIMARY KEY,
    GuestName   TEXT    NOT NULL,
    PhoneNumber TEXT    NOT NULL,
    TemplateId  INTEGER NOT NULL REFERENCES MessageTemplates(Id),
    ParametersJson TEXT NOT NULL DEFAULT '{}',
    TriggerTime TEXT    NOT NULL,
    Status      TEXT    NOT NULL DEFAULT 'Pending',
    CreatedAt   TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS InboxMessages (
    Id              TEXT    PRIMARY KEY,
    FromPhoneNumber TEXT    NOT NULL,
    Content         TEXT    NOT NULL,
    IsRead          INTEGER NOT NULL DEFAULT 0,
    ReceivedAt      TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS AppConfig (
    Key   TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);

INSERT INTO MessageTemplates (Name, ExternalTemplateName, LanguageCode, ChannelType, CreatedAt)
SELECT 'Spa Up-sell Drip', 'spa_promo_1', 'en', 'WhatsApp', datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MessageTemplates WHERE Name = 'Spa Up-sell Drip');

INSERT INTO MessageTemplates (Name, ExternalTemplateName, LanguageCode, ChannelType, CreatedAt)
SELECT 'Checkout Billing', 'checkout_billing', 'en', 'WhatsApp', datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MessageTemplates WHERE Name = 'Checkout Billing');

INSERT INTO AutomationRules (TriggerEventType, TemplateId, IsEnabled, DelayMinutes)
SELECT 'CheckOut', (SELECT Id FROM MessageTemplates WHERE Name = 'Checkout Billing'), 1, 0
WHERE NOT EXISTS (SELECT 1 FROM AutomationRules WHERE TriggerEventType = 'CheckOut' AND DelayMinutes = 0);

INSERT INTO AutomationRules (TriggerEventType, TemplateId, IsEnabled, DelayMinutes)
SELECT 'CheckIn', (SELECT Id FROM MessageTemplates WHERE Name = 'Spa Up-sell Drip'), 1, 1
WHERE NOT EXISTS (SELECT 1 FROM AutomationRules WHERE TriggerEventType = 'CheckIn');
