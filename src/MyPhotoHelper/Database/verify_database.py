import sqlite3
import os

db_path = os.path.join(os.path.dirname(__file__), "dev_facevault.db")
print(f"Checking database: {db_path}")
print()

if not os.path.exists(db_path):
    print("ERROR: Database file does not exist!")
    exit(1)

conn = sqlite3.connect(db_path)
cursor = conn.cursor()

# Check tables
print("Tables in database:")
cursor.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
for table in cursor.fetchall():
    print(f"  - {table[0]}")

print()

# Check settings table structure
print("Settings table structure:")
cursor.execute("PRAGMA table_info(tbl_app_settings)")
for col in cursor.fetchall():
    print(f"  - {col[1]} ({col[2]})")

print()

# Check settings data
print("Settings data (first 10):")
cursor.execute("SELECT SettingName, SettingType, SettingValue FROM tbl_app_settings LIMIT 10")
for row in cursor.fetchall():
    print(f"  - {row[0]}: {row[1]} = '{row[2]}'")

conn.close()
print("\nDatabase verification complete!")