IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Scripts') AND type = 'U')
BEGIN
    CREATE TABLE dbo.Scripts (
        ScriptId        BIGINT IDENTITY(1,1) NOT NULL,
        ApplicationId   NVARCHAR(200)        NOT NULL,
        Name            NVARCHAR(500)        NOT NULL,
        TypeTag         NVARCHAR(100)        NOT NULL,
        Language        NVARCHAR(50)         NOT NULL,
        Content         NVARCHAR(MAX)        NOT NULL,
        Description     NVARCHAR(2000)       NULL,
        IsEnabled       BIT                  NOT NULL DEFAULT 1,
        CreatedBy       NVARCHAR(100)        NOT NULL,
        CreatedDate     DATETIME2            NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedBy      NVARCHAR(100)        NOT NULL,
        ModifiedDate    DATETIME2            NOT NULL DEFAULT SYSUTCDATETIME(),
        Comment         NVARCHAR(4000)       NULL,
        RowVersion      ROWVERSION           NOT NULL,

        CONSTRAINT PK_Scripts PRIMARY KEY CLUSTERED (ScriptId),
        CONSTRAINT UX_Scripts_App_Name UNIQUE (ApplicationId, Name)
    );

    CREATE INDEX IX_Scripts_App_TypeTag ON dbo.Scripts (ApplicationId, TypeTag);
END
