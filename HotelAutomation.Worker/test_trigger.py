import sqlite3
import json
from datetime import datetime, timedelta

conn = sqlite3.connect('hotel_automation.db')
c = conn.cursor()

# Get today's date + 30 days
target_date = datetime.utcnow() + timedelta(days=30)
birthday_str = f"1990-{target_date.strftime('%m-%d')}"

print(f"Setting birthday to {birthday_str} for trigger offset -30")

# Clear outbox
c.execute('DELETE FROM OutboxMessages')

# Insert Contact
c.execute("DELETE FROM Contacts WHERE PhoneNumber = '+19999999999'")
custom_fields = json.dumps({'Birthday': birthday_str})
c.execute("INSERT INTO Contacts (PhoneNumber, FirstName, LastName, CategoryId, CustomFieldsJson, CreatedAt) VALUES (?, ?, ?, ?, ?, ?)", ('+19999999999', 'Test', 'BirthdayBoy', None, custom_fields, '2024-01-01'))

# Insert Template if not exists
c.execute("INSERT OR IGNORE INTO MessageTemplates (Id, Name, ExternalTemplateName, LanguageCode, ChannelType, CreatedAt) VALUES (999, 'Birthday Temp', 'bday_temp', 'en', 'WhatsApp', '2024-01-01')")

# Insert Rule
c.execute("DELETE FROM AutomationRules WHERE TriggerEventType = 'DateTrigger'")
conditions = json.dumps({'Column': 'Birthday', 'OffsetDays': -30, 'IgnoreYear': True})
c.execute("INSERT INTO AutomationRules (TriggerEventType, TemplateId, IsEnabled, ConditionsJson, DelayMinutes) VALUES (?, ?, ?, ?, ?)", ('DateTrigger', 999, 1, conditions, 0))

conn.commit()
conn.close()
print('Test data ready!')
