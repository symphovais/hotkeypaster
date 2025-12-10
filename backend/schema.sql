-- TalkKeys Database Schema

-- Users table
CREATE TABLE IF NOT EXISTS users (
    id TEXT PRIMARY KEY,
    google_id TEXT UNIQUE NOT NULL,
    email TEXT NOT NULL,
    name TEXT,
    created_at INTEGER NOT NULL,
    last_login INTEGER
);

-- Daily usage tracking
CREATE TABLE IF NOT EXISTS usage (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id TEXT NOT NULL,
    date TEXT NOT NULL,
    audio_seconds INTEGER DEFAULT 0,
    requests INTEGER DEFAULT 0,
    FOREIGN KEY (user_id) REFERENCES users(id),
    UNIQUE(user_id, date)
);

-- Index for fast usage lookups
CREATE INDEX IF NOT EXISTS idx_usage_user_date ON usage(user_id, date);
