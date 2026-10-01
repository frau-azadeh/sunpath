-- Run on SunPathDb after 001_FleetTracking.sql. Non-destructive and repeatable.
IF COL_LENGTH('Missions', 'AcceptedAtUtc') IS NULL
BEGIN
    ALTER TABLE Missions ADD AcceptedAtUtc DATETIME2 NULL;
    EXEC(N'UPDATE Missions SET AcceptedAtUtc=COALESCE(StartedAtUtc,CreatedAtUtc,SYSUTCDATETIME()) WHERE Status IN (2,3);');
END;
IF COL_LENGTH('Missions', 'ArrivedAtUtc') IS NULL
    ALTER TABLE Missions ADD ArrivedAtUtc DATETIME2 NULL;
IF OBJECT_ID('MissionNotifications', 'U') IS NULL
BEGIN
    CREATE TABLE MissionNotifications (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Audience NVARCHAR(10) NOT NULL,
        DriverId INT NULL,
        MissionId INT NOT NULL,
        Kind NVARCHAR(20) NOT NULL,
        Title NVARCHAR(300) NOT NULL,
        Message NVARCHAR(1000) NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_MissionNotifications_Created DEFAULT SYSUTCDATETIME(),
        ReadAtUtc DATETIME2 NULL,
        CONSTRAINT CK_MissionNotifications_Audience CHECK
            ((Audience='admin' AND DriverId IS NULL) OR (Audience='driver' AND DriverId IS NOT NULL))
    );
    CREATE INDEX IX_MissionNotifications_Inbox ON MissionNotifications(Audience, DriverId, Id DESC) INCLUDE(ReadAtUtc);
END;

-- Existing active assignments receive one durable inbox item on installation.
INSERT INTO MissionNotifications (Audience,DriverId,MissionId,Kind,Title,Message)
SELECT 'driver',m.DriverId,m.Id,'assigned',N'مأموریت فعال شما',COALESCE(m.Title,N'مأموریت')
FROM Missions m
WHERE m.DriverId IS NOT NULL AND m.Status IN (1,2)
AND NOT EXISTS (SELECT 1 FROM MissionNotifications n WHERE n.MissionId=m.Id AND n.DriverId=m.DriverId AND n.Kind='assigned');
