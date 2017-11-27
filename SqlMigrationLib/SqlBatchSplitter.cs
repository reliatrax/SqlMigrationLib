using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SqlMigrationLib
{
    static class SqlBatchSplitter
    {
        static public string[] SplitBatches(string sql)
        {
            string[] batches = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

            return batches;
        }
    }
}
