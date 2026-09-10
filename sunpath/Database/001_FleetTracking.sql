/*
    SunPath Fleet Tracking - migration
    قبل از اجرای برنامه روی SunPathDb اجرا شود.
    این اسکریپت ساختار فعلی را حفظ می‌کند و فقط ستون/جدول لازم را اضافه می‌کند.
*/
IF COL_LENGTH('Drivers', 'Username') IS NULL
    ALTER TABLE Drivers ADD Username NVARCHAR(100) NULL;
IF COL_LENGTH('Drivers', 'PasswordHash') IS NULL
    ALTER TABLE Drivers ADD PasswordHash NVARCHAR(500) NULL;
IF COL_LENGTH('Drivers', 'IsActive') IS NULL
    ALTER TABLE Drivers ADD IsActive BIT NOT NULL CONSTRAINT DF_Drivers_IsActive DEFAULT(1);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Drivers_Username' AND object_id=OBJECT_ID('Drivers'))
    CREATE UNIQUE INDEX UX_Drivers_Username ON Drivers(Username) WHERE Username IS NOT NULL;

IF OBJECT_ID('VehicleLocationHistory','U') IS NULL
BEGIN
    CREATE TABLE VehicleLocationHistory
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        VehicleId INT NOT NULL,
        DriverId INT NULL,
        MissionId INT NULL,
        Latitude DECIMAL(10,7) NOT NULL,
        Longitude DECIMAL(10,7) NOT NULL,
        Accuracy DECIMAL(10,2) NULL,
        Speed DECIMAL(10,2) NULL,
        Heading DECIMAL(10,2) NULL,
        RecordedAtUtc DATETIME2 NOT NULL
    );
    CREATE INDEX IX_VehicleLocationHistory_Vehicle_Time
        ON VehicleLocationHistory(VehicleId, RecordedAtUtc DESC);
END

IF COL_LENGTH('Vehicles','Latitude') IS NULL
    ALTER TABLE Vehicles ADD Latitude FLOAT NULL;
IF COL_LENGTH('Vehicles','Longitude') IS NULL
    ALTER TABLE Vehicles ADD Longitude FLOAT NULL;
IF COL_LENGTH('Vehicles','LastLatitude') IS NULL
    ALTER TABLE Vehicles ADD LastLatitude DECIMAL(10,7) NULL;
IF COL_LENGTH('Vehicles','LastLongitude') IS NULL
    ALTER TABLE Vehicles ADD LastLongitude DECIMAL(10,7) NULL;
IF COL_LENGTH('Vehicles','Speed') IS NULL
    ALTER TABLE Vehicles ADD Speed FLOAT NOT NULL CONSTRAINT DF_Vehicles_Speed DEFAULT(0);
IF COL_LENGTH('Vehicles','Heading') IS NULL
    ALTER TABLE Vehicles ADD Heading FLOAT NOT NULL CONSTRAINT DF_Vehicles_Heading DEFAULT(0);
IF COL_LENGTH('Vehicles','LastUpdate') IS NULL
    ALTER TABLE Vehicles ADD LastUpdate DATETIME NULL;
IF COL_LENGTH('Vehicles','LastUpdateAt') IS NULL
    ALTER TABLE Vehicles ADD LastUpdateAt DATETIME NULL;
IF COL_LENGTH('Vehicles','CurrentDriverId') IS NULL
    ALTER TABLE Vehicles ADD CurrentDriverId INT NULL;
IF COL_LENGTH('Vehicles','FuelConsumedLiters') IS NULL
    ALTER TABLE Vehicles ADD FuelConsumedLiters FLOAT NOT NULL CONSTRAINT DF_Vehicles_Fuel DEFAULT(0);
IF COL_LENGTH('Vehicles','TripDistanceKm') IS NULL
    ALTER TABLE Vehicles ADD TripDistanceKm FLOAT NOT NULL CONSTRAINT DF_Vehicles_TripDistance DEFAULT(0);
IF COL_LENGTH('Vehicles','TripDurationSeconds') IS NULL
    ALTER TABLE Vehicles ADD TripDurationSeconds INT NOT NULL CONSTRAINT DF_Vehicles_TripDuration DEFAULT(0);
IF COL_LENGTH('Vehicles','StopDurationSeconds') IS NULL
    ALTER TABLE Vehicles ADD StopDurationSeconds INT NOT NULL CONSTRAINT DF_Vehicles_StopDuration DEFAULT(0);

-- Missions باید با این ستون‌ها موجود باشد؛ اگر دیتابیس فعلی این جدول را دارد فقط ستون‌های لازم را اضافه کن.
IF OBJECT_ID('Missions','U') IS NULL
BEGIN
    CREATE TABLE Missions
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        DriverId INT NULL,
        VehicleId INT NOT NULL,
        Title NVARCHAR(200) NULL,
        Description NVARCHAR(1000) NULL,
        OriginTitle NVARCHAR(300) NULL,
        OriginLatitude DECIMAL(10,7) NULL,
        OriginLongitude DECIMAL(10,7) NULL,
        DestinationTitle NVARCHAR(300) NULL,
        DestinationLatitude DECIMAL(10,7) NULL,
        DestinationLongitude DECIMAL(10,7) NULL,
        Status INT NOT NULL DEFAULT(1),
        StartedAtUtc DATETIME2 NULL,
        CompletedAtUtc DATETIME2 NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT(GETUTCDATE()),
        UpdatedAtUtc DATETIME2 NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Missions_ActiveVehicle' AND object_id=OBJECT_ID('Missions'))
    CREATE INDEX IX_Missions_ActiveVehicle ON Missions(VehicleId, Status, Id DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Missions_ActiveDriver' AND object_id=OBJECT_ID('Missions'))
    CREATE INDEX IX_Missions_ActiveDriver ON Missions(DriverId, Status, Id DESC);
