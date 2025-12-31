IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE TABLE [OutboxMessages] (
        [Id] uniqueidentifier NOT NULL,
        [EventType] nvarchar(500) NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [CorrelationId] nvarchar(100) NOT NULL,
        [TenantId] nvarchar(50) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ProcessedAt] datetime2 NULL,
        [RetryCount] int NOT NULL,
        [Error] nvarchar(2000) NULL,
        [Status] int NOT NULL,
        CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE TABLE [Permissions] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Resource] nvarchar(100) NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [TenantId] nvarchar(50) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedBy] nvarchar(256) NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [IsSystemRole] bit NOT NULL,
        [TenantId] nvarchar(50) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedBy] nvarchar(256) NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Subdomain] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [ContactEmail] nvarchar(256) NOT NULL,
        [ContactPhone] nvarchar(50) NULL,
        [IsDeleted] bit NOT NULL,
        [Settings] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [PasswordHash] nvarchar(512) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [TenantId] nvarchar(50) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedBy] nvarchar(256) NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [RoleId] uniqueidentifier NOT NULL,
        [PermissionId] uniqueidentifier NOT NULL,
        [AssignedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
        CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE TABLE [UserPermissions] (
        [UserId] uniqueidentifier NOT NULL,
        [PermissionId] uniqueidentifier NOT NULL,
        [AssignedAt] datetime2 NOT NULL,
        [AssignedBy] nvarchar(256) NOT NULL,
        CONSTRAINT [PK_UserPermissions] PRIMARY KEY ([UserId], [PermissionId]),
        CONSTRAINT [FK_UserPermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserPermissions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [AssignedAt] datetime2 NOT NULL,
        [AssignedBy] nvarchar(256) NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_CorrelationId] ON [OutboxMessages] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_ProcessedAt] ON [OutboxMessages] ([ProcessedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_Status] ON [OutboxMessages] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_Status_CreatedAt] ON [OutboxMessages] ([Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_TenantId] ON [OutboxMessages] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Permissions_IsDeleted] ON [Permissions] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Permissions_Resource_Action] ON [Permissions] ([Resource], [Action]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Permissions_TenantId] ON [Permissions] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_TenantId_Name] ON [Permissions] ([TenantId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_RoleId] ON [RolePermissions] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Roles_IsDeleted] ON [Roles] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Roles_TenantId] ON [Roles] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_TenantId_Name] ON [Roles] ([TenantId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tenants_IsActive] ON [Tenants] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tenants_IsDeleted] ON [Tenants] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tenants_Name] ON [Tenants] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tenants_Subdomain] ON [Tenants] ([Subdomain]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserPermissions_PermissionId] ON [UserPermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserPermissions_UserId] ON [UserPermissions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserRoles_UserId] ON [UserRoles] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_IsDeleted] ON [Users] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_TenantId] ON [Users] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_TenantId_Email] ON [Users] ([TenantId], [Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231063043_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251231063043_InitialCreate', N'9.0.1');
END;

COMMIT;
GO

