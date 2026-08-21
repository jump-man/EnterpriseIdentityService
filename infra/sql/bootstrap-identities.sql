:setvar RuntimeIdentityName "replace-with-runtime-identity-name"
:setvar MigrationIdentityName "replace-with-migration-identity-name"

-- Run once against the application database while authenticated as the configured
-- Microsoft Entra administrator. Keep the two identities separate: the API never
-- receives schema-change membership, and the deployment job is not used at runtime.

IF DATABASE_PRINCIPAL_ID(N'$(RuntimeIdentityName)') IS NULL
BEGIN
    CREATE USER [$(RuntimeIdentityName)] FROM EXTERNAL PROVIDER;
END;

ALTER ROLE db_datareader ADD MEMBER [$(RuntimeIdentityName)];
ALTER ROLE db_datawriter ADD MEMBER [$(RuntimeIdentityName)];

IF DATABASE_PRINCIPAL_ID(N'$(MigrationIdentityName)') IS NULL
BEGIN
    CREATE USER [$(MigrationIdentityName)] FROM EXTERNAL PROVIDER;
END;

ALTER ROLE db_datareader ADD MEMBER [$(MigrationIdentityName)];
ALTER ROLE db_datawriter ADD MEMBER [$(MigrationIdentityName)];
ALTER ROLE db_ddladmin ADD MEMBER [$(MigrationIdentityName)];
