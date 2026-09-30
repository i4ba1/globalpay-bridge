-- Creates the application database if it does not exist yet.
-- Run this before `dotnet ef database update` when the database has never been created:
-- a connection string that names a non-existent database fails at login (SQL error 18456, state 38),
-- so the schema cannot be bootstrapped from an empty server in a single step.
IF DB_ID(N'GlobalPayBridge') IS NULL
BEGIN
    PRINT 'Creating database GlobalPayBridge';
    CREATE DATABASE GlobalPayBridge;
END
ELSE
BEGIN
    PRINT 'Database GlobalPayBridge already exists';
END;
GO
