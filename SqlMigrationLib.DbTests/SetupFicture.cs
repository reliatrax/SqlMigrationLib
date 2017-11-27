using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlMigrationLib.DbTests
{
    [SetUpFixture]      // marks this class as our one-time setup class to be run before any other tests from this namespace
    public class SetupFicture
    {
        [OneTimeSetUp]
        public void SetupDB()
        {
            // Build / clear the database and add the seed data (a single row with MigrationID 100)
            DBUtils.SetupDatabase();
        }
    }
}
