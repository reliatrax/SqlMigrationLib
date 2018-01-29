using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlMigrationLib.DbTests
{
    /// <summary>
    /// Model for database table dbo.Migrations
    /// </summary>
    public class MigrationHistory
    {
        public int MigrationID { get; set; }
        public DateTime UpdateUTC { get; set; }

        public MigrationHistory() { }

        public MigrationHistory(int migrationID, DateTime updateUTC)
        {
            MigrationID = migrationID;
            UpdateUTC = updateUTC;
        }
    }

    /// <summary>
    /// Model for database table dbo.Migrations
    /// </summary>
    public class Message
    {
        public int MessageID { get; set; }
        public string MessageText { get; set; }
    }
}
