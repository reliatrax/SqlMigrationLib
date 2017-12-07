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
            string[] batches = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);       // GO must appear on a line by itself, with no other punctuation

            // Skip empty batches
            batches = batches.Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

            return batches;
        }
    }
}
