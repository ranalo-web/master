-- Passwords are now stored hashed (Services/PasswordRules.cs), about 84
-- characters. Widens Users.PasswordHash if it's shorter than that; existing
-- passwords are kept and hashed the next time each person logs in.
--
-- RUN THIS BEFORE DEPLOYING THE APP. Safe to re-run.

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'PasswordHash'
      AND max_length <> -1                      -- not already (N)VARCHAR(MAX)
      AND max_length < 512                      -- NVARCHAR(256) = 512 bytes
)
BEGIN
    DECLARE @nullable BIT = (SELECT is_nullable FROM sys.columns
                             WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'PasswordHash');
    IF @nullable = 1
        ALTER TABLE dbo.Users ALTER COLUMN PasswordHash NVARCHAR(256) NULL;
    ELSE
        ALTER TABLE dbo.Users ALTER COLUMN PasswordHash NVARCHAR(256) NOT NULL;
END
GO

-- How many still need hashing (they convert as people log in).
SELECT COUNT(*) AS PasswordsNotYetHashed
FROM dbo.Users
WHERE PasswordHash IS NOT NULL AND (LEN(PasswordHash) < 80 OR LEFT(PasswordHash, 4) <> 'AQAA');
