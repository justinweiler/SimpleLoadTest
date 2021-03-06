/******************************************************************************

NOTES:	* The path for your SQL instance below may have to change per your environment.
		* You MUST re-set the password in SSMS for your user post deploying (password in this script)

*******************************************************************************/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE DATABASE [BasicDBLoadTest]
 ON  PRIMARY 
( NAME = N'BasicDBLoadTest', FILENAME = N'C:\DATA\BasicDBLoadTest.mdf' , SIZE = 64512KB , MAXSIZE = UNLIMITED, FILEGROWTH = 1024KB )
 LOG ON 
( NAME = N'BasicDBLoadTest_log', FILENAME = N'C:\DATA\BasicDBLoadTest_log.ldf' , SIZE = 1623488KB , MAXSIZE = 2048GB , FILEGROWTH = 10%)
GO

ALTER DATABASE [BasicDBLoadTest] SET COMPATIBILITY_LEVEL = 100
GO

IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
begin
EXEC [BasicDBLoadTest].[dbo].[sp_fulltext_database] @action = 'enable'
end
GO

ALTER DATABASE [BasicDBLoadTest] SET ANSI_NULL_DEFAULT OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET ANSI_NULLS OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET ANSI_PADDING OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET ANSI_WARNINGS OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET ARITHABORT OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET AUTO_CLOSE OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET AUTO_SHRINK OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET AUTO_UPDATE_STATISTICS ON 
GO

ALTER DATABASE [BasicDBLoadTest] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET CURSOR_DEFAULT  GLOBAL 
GO

ALTER DATABASE [BasicDBLoadTest] SET CONCAT_NULL_YIELDS_NULL OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET NUMERIC_ROUNDABORT OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET QUOTED_IDENTIFIER OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET RECURSIVE_TRIGGERS OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET  DISABLE_BROKER 
GO

ALTER DATABASE [BasicDBLoadTest] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET TRUSTWORTHY OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET ALLOW_SNAPSHOT_ISOLATION OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET PARAMETERIZATION SIMPLE 
GO

ALTER DATABASE [BasicDBLoadTest] SET READ_COMMITTED_SNAPSHOT OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET HONOR_BROKER_PRIORITY OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET RECOVERY FULL 
GO

ALTER DATABASE [BasicDBLoadTest] SET  MULTI_USER 
GO

ALTER DATABASE [BasicDBLoadTest] SET PAGE_VERIFY CHECKSUM  
GO

ALTER DATABASE [BasicDBLoadTest] SET DB_CHAINING OFF 
GO

ALTER DATABASE [BasicDBLoadTest] SET  READ_WRITE 
GO

CREATE LOGIN [testUser] WITH PASSWORD='W1shb0n3', DEFAULT_DATABASE=[BasicDBLoadTest], DEFAULT_LANGUAGE=[us_english]
GO

USE [BasicDBLoadTest]
GO

/****** Object:  User [testUser]    Script Date: 9/11/2012 11:54:47 AM ******/
CREATE USER [testUser] FOR LOGIN [testUser] WITH DEFAULT_SCHEMA=[dbo]
GO

EXEC sp_addrolemember [db_owner],[testUser]
GO

EXEC sp_addrolemember [db_datareader],[testUser]
GO

EXEC sp_addrolemember [db_datawriter],[testUser]
GO

GRANT CONNECT TO [testUser]
GO

CREATE TABLE [dbo].[Customer](
	[Id] [uniqueidentifier] NOT NULL,
	[FirstName] [varchar](256) NOT NULL,
	[LastName] [varchar](256) NOT NULL,
	[DateActive] [datetime] NOT NULL,
	[Payload] [varchar](8000) NULL,
 CONSTRAINT [PK_Customer] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

CREATE NONCLUSTERED INDEX [IX_Customer_DateActive] ON [dbo].[Customer]
(
	[DateActive] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

CREATE NONCLUSTERED INDEX [IX_Customer_LastName] ON [dbo].[Customer]
(
	[LastName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

CREATE PROCEDURE [dbo].[Customer_DeleteAllRecords] 
AS
BEGIN	
	BEGIN TRANSACTION Customer_DeleteAllRecords 
		
    DELETE
	FROM 
		Customer

	IF @@ERROR = 0 
		BEGIN
			COMMIT TRANSACTION Customer_DeleteAllRecords
			RETURN 1
		END
	ELSE
		BEGIN
			ROLLBACK TRANSACTION Customer_DeleteAllRecords
			RETURN -1
		END
END
GO

CREATE PROCEDURE [dbo].[Customer_Insert]
	@Id uniqueidentifier, 
	@FirstName varchar(256),
	@LastName varchar(256), 
	@DateActive datetime, 
	@Payload varchar(8000)

AS
BEGIN
	BEGIN TRANSACTION customerInsert

	INSERT INTO 
		Customer 
		(
		Id,
		FirstName, 
		LastName, 
		DateActive, 
		Payload
		)
	VALUES
		(
		@Id,
		@FirstName,
		@LastName, 
		@DateActive, 
		@Payload
		) 

	IF @@ERROR = 0 
		BEGIN
			COMMIT TRANSACTION customerInsert
			RETURN 1
		END
	ELSE
		BEGIN
			ROLLBACK TRANSACTION customerInsert
			RETURN -1
		END
END
GO

CREATE PROCEDURE [dbo].[Customer_Select]
	@Id uniqueidentifier
AS
BEGIN
	SET NOCOUNT ON;
	    
	SELECT 
		Id, 
		FirstName, 
		LastName, 
		DateActive, 
		Payload
	FROM
		Customer	
	WHERE
		Customer.Id = @Id
END
GO