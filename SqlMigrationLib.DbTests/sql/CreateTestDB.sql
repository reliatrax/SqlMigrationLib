USE master
GO

--drop database JunkDB

CREATE DATABASE JunkDB CONTAINMENT=PARTIAL
ON (NAME='Data',
	FILENAME='C:\CCS\DBBackups\junk.mdf',
	SIZE=4, FILEGROWTH=10%)
LOG ON (NAME='Log',
	FILENAME='C:\CCS\DBBackups\junk.ldf',
	SIZE=4, FILEGROWTH=10%)	
GO


CREATE TABLE dbo.Persons
(
	PersonID INT IDENTITY(1,1) NOT NULL,		-- unique id for this record
	
	FirstName VARCHAR(12) NOT NULL,
	LastName VARCHAR(20) NOT NULL,
	
	CONSTRAINT PK_Tickets PRIMARY KEY CLUSTERED  (PersonID ASC),
)

select * from dbo.Persons

SELECT count(*)
FROM master.dbo.sysdatabases 
WHERE ('[' + name + ']' = 'SqlMigrationLibTestDB' OR name = 'SqlMigrationLibTestDB')


CREATE TABLE dbo.Migrations
(
	MigrationID INT NOT NULL,		-- Primary key
	
	UpdateDT DateTime NOT NULL,	-- UTC Time this migration was run
		
	CONSTRAINT PK_MigrationID PRIMARY KEY CLUSTERED  (MigrationID ASC),
)
