IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.ScriptHistory') AND type = 'U')
BEGIN
    CREATE TABLE dbo.ScriptHistory (
        HistoryId          BIGINT IDENTITY(1,1) NOT NULL,
        ScriptId           BIGINT               NOT NULL,
        ApplicationId      NVARCHAR(200)        NOT NULL,
        Name               NVARCHAR(500)        NOT NULL,
        OldContent         NVARCHAR(MAX)        NULL,
        NewContent         NVARCHAR(MAX)        NULL,
        OldIsEnabled       BIT                  NULL,
        NewIsEnabled       BIT                  NULL,
        RowVersionBefore   VARBINARY(8)         NULL,
        RowVersionAfter    VARBINARY(8)         NULL,
        ChangedBy          NVARCHAR(100)        NOT NULL,
        ChangedDate        DATETIME2            NOT NULL DEFAULT SYSUTCDATETIME(),
        Operation          NVARCHAR(50)         NOT NULL,
        Comment            NVARCHAR(4000)       NULL,

        CONSTRAINT PK_ScriptHistory PRIMARY KEY CLUSTERED (HistoryId)
    );

    CREATE INDEX IX_ScriptHistory_ScriptId ON dbo.ScriptHistory (ScriptId);
    CREATE INDEX IX_ScriptHistory_AppNameDate ON dbo.ScriptHistory (ApplicationId, Name, ChangedDate);
END
