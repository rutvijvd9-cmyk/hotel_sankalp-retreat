-- HotelAutomation SQLite Schema
-- All statements use IF NOT EXISTS so running this multiple times is safe.

CREATE TABLE IF NOT EXISTS MessageTemplates (
    Id                   INTEGER PRIMARY KEY AUTOINCREMENT,
    Name                 TEXT    NOT NULL,
    ExternalTemplateName TEXT    NOT NULL,
    LanguageCode         TEXT    NOT NULL,
    ChannelType          TEXT    NOT NULL,
    BodyParamCount       INTEGER NOT NULL DEFAULT -1,
    FallbackChannelType  TEXT,
    CreatedAt            TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS AutomationRules (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    Name             TEXT    NULL,
    Description      TEXT    NULL,
    TriggerEventType TEXT    NOT NULL,
    TemplateId       INTEGER NOT NULL REFERENCES MessageTemplates(Id),
    IsEnabled        INTEGER NOT NULL DEFAULT 1,
    ConditionsJson   TEXT    NULL,
    AudienceFilter   TEXT    NULL,
    DelayMinutes     INTEGER NOT NULL DEFAULT 0,
    TemplateWeightsJson TEXT NULL
);

CREATE TABLE IF NOT EXISTS OutboxMessages (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    ToPhoneNumber  TEXT    NOT NULL,
    TemplateId     INTEGER NOT NULL REFERENCES MessageTemplates(Id),
    ParametersJson TEXT    NOT NULL,
    Status         TEXT    NOT NULL DEFAULT 'Pending',
    RetryCount     INTEGER NOT NULL DEFAULT 0,
    CreatedAt      TEXT    NOT NULL,
    SentAt         TEXT,
    ExternalMessageId TEXT
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

CREATE TABLE IF NOT EXISTS ContactCategories (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Name        TEXT    NOT NULL UNIQUE,
    Description TEXT,
    CreatedAt   TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS Contacts (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    PhoneNumber      TEXT    NOT NULL UNIQUE,
    FirstName        TEXT,
    LastName         TEXT,
    CategoryId       INTEGER REFERENCES ContactCategories(Id) ON DELETE SET NULL,
    CustomFieldsJson TEXT    NOT NULL DEFAULT '{}',
    CreatedAt        TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS Campaigns (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    Name          TEXT    NOT NULL,
    TemplateId    INTEGER NOT NULL REFERENCES MessageTemplates(Id),
    TotalContacts INTEGER NOT NULL DEFAULT 0,
    Status        TEXT    NOT NULL DEFAULT 'Processing',
    CreatedAt     TEXT    NOT NULL
);

