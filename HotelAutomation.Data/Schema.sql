-- HotelAutomation SQLite Schema
-- All statements use IF NOT EXISTS so running this multiple times is safe.

CREATE TABLE IF NOT EXISTS MessageTemplates (
    Id                   INTEGER PRIMARY KEY AUTOINCREMENT,
    Name                 TEXT    NOT NULL,
    ExternalTemplateName TEXT    NOT NULL,
    LanguageCode         TEXT    NOT NULL,
    ChannelType          TEXT    NOT NULL,
    FallbackChannelType  TEXT,
    CreatedAt            TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS AutomationRules (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    TriggerEventType TEXT    NOT NULL,
    TemplateId       INTEGER NOT NULL REFERENCES MessageTemplates(Id),
    IsEnabled        INTEGER NOT NULL DEFAULT 1,
    ConditionsJson   TEXT    NULL,
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

-- ───────────────────────────────────────────────
-- Seed: Core Hotel Templates
-- ───────────────────────────────────────────────

-- 1. Guest Check-In Welcome
INSERT INTO MessageTemplates (Name, ExternalTemplateName, LanguageCode, ChannelType, CreatedAt)
SELECT 'CheckIn Welcome', 'hotel_checkin_welcome', 'en', 'WhatsApp', datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MessageTemplates WHERE Name = 'CheckIn Welcome');

-- 2. Checkout Bill Summary
INSERT INTO MessageTemplates (Name, ExternalTemplateName, LanguageCode, ChannelType, FallbackChannelType, CreatedAt)
SELECT 'Checkout Bill Summary', 'hotel_checkout_bill', 'en', 'WhatsApp', 'SMS', datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MessageTemplates WHERE Name = 'Checkout Bill Summary');

-- 3. Post-Stay Feedback Request (sent 24h after checkout)
INSERT INTO MessageTemplates (Name, ExternalTemplateName, LanguageCode, ChannelType, CreatedAt)
SELECT 'Post-Stay Feedback Request', 'hotel_feedback_request', 'en', 'WhatsApp', datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MessageTemplates WHERE Name = 'Post-Stay Feedback Request');

-- 4. Marketing Offer (used for Broadcast campaigns)
INSERT INTO MessageTemplates (Name, ExternalTemplateName, LanguageCode, ChannelType, CreatedAt)
SELECT 'Seasonal Marketing Offer', 'hotel_seasonal_offer', 'en', 'WhatsApp', datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MessageTemplates WHERE Name = 'Seasonal Marketing Offer');

-- Legacy templates (kept for backwards compatibility)
INSERT INTO MessageTemplates (Name, ExternalTemplateName, LanguageCode, ChannelType, CreatedAt)
SELECT 'Spa Up-sell Drip Variant A', 'spa_promo_1', 'en', 'WhatsApp', datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MessageTemplates WHERE Name = 'Spa Up-sell Drip Variant A');

INSERT INTO MessageTemplates (Name, ExternalTemplateName, LanguageCode, ChannelType, CreatedAt)
SELECT 'Spa Up-sell Drip Variant B', 'spa_promo_1_var_b', 'en', 'WhatsApp', datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MessageTemplates WHERE Name = 'Spa Up-sell Drip Variant B');

INSERT INTO MessageTemplates (Name, ExternalTemplateName, LanguageCode, ChannelType, FallbackChannelType, CreatedAt)
SELECT 'Checkout Billing', 'checkout_billing', 'en_US', 'WhatsApp', 'SMS', datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MessageTemplates WHERE Name = 'Checkout Billing');

-- ───────────────────────────────────────────────
-- Seed: Automation Rules  
-- ───────────────────────────────────────────────

-- Rule: CheckIn → Welcome message (immediate)
INSERT INTO AutomationRules (TriggerEventType, TemplateId, IsEnabled, DelayMinutes)
SELECT 'CheckIn', (SELECT Id FROM MessageTemplates WHERE Name = 'CheckIn Welcome'), 1, 0
WHERE NOT EXISTS (SELECT 1 FROM AutomationRules WHERE TriggerEventType = 'CheckIn' AND DelayMinutes = 0);

-- Rule: CheckOut → Bill Summary (immediate)
INSERT INTO AutomationRules (TriggerEventType, TemplateId, IsEnabled, DelayMinutes)
SELECT 'CheckOut', (SELECT Id FROM MessageTemplates WHERE Name = 'Checkout Bill Summary'), 1, 0
WHERE NOT EXISTS (SELECT 1 FROM AutomationRules WHERE TriggerEventType = 'CheckOut' AND DelayMinutes = 0);

-- Rule: CheckOut → Feedback Request (24 hours after checkout = 1440 minutes)
INSERT INTO AutomationRules (TriggerEventType, TemplateId, IsEnabled, DelayMinutes)
SELECT 'CheckOut', (SELECT Id FROM MessageTemplates WHERE Name = 'Post-Stay Feedback Request'), 1, 1440
WHERE NOT EXISTS (SELECT 1 FROM AutomationRules WHERE TriggerEventType = 'CheckOut' AND DelayMinutes = 1440);

-- Rule: Legacy spa drip (kept for backwards compatibility with A/B test)
INSERT INTO AutomationRules (TriggerEventType, TemplateId, IsEnabled, DelayMinutes, TemplateWeightsJson)
SELECT 'CheckIn', 
       (SELECT Id FROM MessageTemplates WHERE Name = 'Spa Up-sell Drip Variant A'), 
       0,   -- disabled by default, was the legacy rule
       1, 
       '{"1": 50, "2": 50}'
WHERE NOT EXISTS (SELECT 1 FROM AutomationRules WHERE TriggerEventType = 'CheckIn' AND DelayMinutes = 1);


