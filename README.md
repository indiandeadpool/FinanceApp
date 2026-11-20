# FinanceApp

## Local accounts and finance data

The desktop app stores account records and separate per-account finance databases in `%LOCALAPPDATA%\VaultFinance`, outside the application folder. The mobile app uses its platform's private app-data directory. Passwords are stored as salted PBKDF2 hashes, not as plain text. Finance records are stored locally and are not uploaded by the app.

When sharing the app, distribute the application files only; do not copy or share the `VaultFinance` local data folder. The desktop app offers account creation first on each launch, with a separate option to unlock an existing account. Local SQLite data is not encrypted at rest, so anyone with access to the same Windows user account or the data files can read it outside the app.
