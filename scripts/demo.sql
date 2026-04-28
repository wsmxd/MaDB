CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    email TEXT NOT NULL UNIQUE
);

INSERT OR IGNORE INTO users(name, email) VALUES ('Alice', 'alice@example.com');
INSERT OR IGNORE INTO users(name, email) VALUES ('Bob', 'bob@example.com');
SELECT id, name, email FROM users ORDER BY id;
