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
    Category             TEXT    NOT NULL DEFAULT 'Utility',
    HeaderType           TEXT    NOT NULL DEFAULT 'None',
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
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ToPhoneNumber TEXT NOT NULL,
    TemplateId INTEGER NOT NULL REFERENCES MessageTemplates(Id),
    VariantId INTEGER REFERENCES TemplateVariants(Id),
    ParametersJson TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Pending',
    SentAt TEXT NULL,
    ErrorMessage TEXT NULL,
    RetryCount INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    RuleId INTEGER NULL REFERENCES AutomationRules(Id),
    ExternalMessageId TEXT NULL
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
    SenderName      TEXT    NULL,
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
    CreatedAt        TEXT    NOT NULL,
    UpdatedAt        TEXT    NOT NULL DEFAULT (datetime('now')),
    UpdateCount      INTEGER NOT NULL DEFAULT 0,
    CheckInCount     INTEGER NOT NULL DEFAULT 0,
    AverageStayDays  REAL    NOT NULL DEFAULT 0,
    IsVip            INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS Campaigns (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    Name          TEXT    NOT NULL,
    IsEnabled     INTEGER NOT NULL DEFAULT 1,
    CreatedAt     TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS CampaignSteps (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    CampaignId    INTEGER NOT NULL REFERENCES Campaigns(Id),
    StepNumber    INTEGER NOT NULL,
    TemplateId    INTEGER NOT NULL REFERENCES MessageTemplates(Id),
    DelayMinutes  INTEGER NOT NULL,
    CreatedAt     TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS ContactCampaignProgress (
    ContactId         INTEGER NOT NULL,
    CampaignStepId    INTEGER NOT NULL,
    Status            TEXT    NOT NULL DEFAULT 'Pending',
    NextExecutionTime TEXT    NOT NULL,
    PRIMARY KEY (ContactId, CampaignStepId)
);

CREATE TABLE IF NOT EXISTS SmartLists (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ListName TEXT NOT NULL,
    FilterConditionsJson TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ContactConsent (
    ContactId INTEGER NOT NULL REFERENCES Contacts(Id),
    CategoryId INTEGER NOT NULL REFERENCES ContactCategories(Id),
    IsSubscribed INTEGER NOT NULL DEFAULT 1,
    UpdatedAt TEXT NOT NULL,
    PRIMARY KEY (ContactId, CategoryId)
);

CREATE TABLE IF NOT EXISTS PmsMappings (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PmsSystemName TEXT NOT NULL,
    JsonPathMapping TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS TemplateVariants (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TemplateId INTEGER NOT NULL REFERENCES MessageTemplates(Id),
    VariantName TEXT NOT NULL,
    Body TEXT NOT NULL,
    Weight INTEGER NOT NULL DEFAULT 50,
    CreatedAt TEXT NOT NULL
);
