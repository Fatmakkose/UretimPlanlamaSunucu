using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UretimPlanlama.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Orders]') AND name = N'AsortiSpecialCode')
                    ALTER TABLE [Orders] ADD [AsortiSpecialCode] nvarchar(MAX) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Orders]') AND name = N'OpenSpecialCode')
                    ALTER TABLE [Orders] ADD [OpenSpecialCode] nvarchar(MAX) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Orders]') AND name = N'PackingListJson')
                    ALTER TABLE [Orders] ADD [PackingListJson] nvarchar(MAX) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Orders]') AND name = N'TalosTestJson')
                    ALTER TABLE [Orders] ADD [TalosTestJson] nvarchar(MAX) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Orders]') AND name = N'PlannedCuttingJson')
                    ALTER TABLE [Orders] ADD [PlannedCuttingJson] nvarchar(MAX) NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // These columns may have been added by Program.cs auto-migration,
            // so we don't drop them in Down to avoid issues.
        }
    }
}
