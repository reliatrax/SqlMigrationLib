using FluentAssertions;
using NUnit.Framework;
using SqlMigrationLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlMigrationLib.Tests
{
    [TestFixture]
    public class CommentStripperTests
    {
        #region Single Line Comments

        [Test]
        public void SingleLineComment_ShouldBeRemovedLeavingANewLine()
        {
            string sql = "--comment\n";

            string result = CommentStripper.ProcessSql(sql);

            result.Should().Be("\n");
        }

        [Test]
        public void SingleLineComment_ShouldEndAtFirstNewLine()
        {
            string sql = "--comment\n\n";

            string result = CommentStripper.ProcessSql(sql);

            result.Should().Be("\n\n");
        }

        [Test]
        public void SingleLineComment_ShouldPreserveBeforeAndAfter()
        {
            string sql = "before\n--comment\n after\n";

            string result = CommentStripper.ProcessSql(sql);

            result.Should().Be("before\n\n after\n");
        }

        [Test]
        public void SingleLineComment_ShouldEndAtEOF()
        {
            string sql = "before\n--comment";

            string result = CommentStripper.ProcessSql(sql);

            result.Should().Be("before\n");
        }

        [Test]
        public void SingleLine_SingleDashShouldNotConfuse()
        {
            string sql = "before\n-not a comment";

            string result = CommentStripper.ProcessSql(sql);

            result.Should().Be(sql);        // should not be changed!
        }

        [Test]
        public void SingleLine_MultipleCommentsAndSingleDashes()
        {
            string sql = "before\n--comment\n a - b; c - d; --comment\n after";

            string result = CommentStripper.ProcessSql(sql);

            string expected = "before\n\n a - b; c - d; \n after";

            result.Should().Be(expected);        // should not be changed!
        }

        [Test]
        public void SingleLineComment_ASingleDashShouldBePreserved()
        {
            string sql = "-";   // this test really has to do with our particular state machine implementation

            string result = CommentStripper.ProcessSql(sql);

            result.Should().Be("-");
        }

        [Test]
        public void SingleLine_NestedSlashStarsShouldBeIgnored()
        {
            string sql = "a--/*...*/ */ /* /* \n b";

            string result = CommentStripper.ProcessSql(sql);

            string expected = "a\n b";

            result.Should().Be(expected);        // should not be changed!
        }

        #endregion

        #region Multi Line Comments

        [Test]
        public void MultiLine_ShouldBeRemoved()
        {
            string sql = "/*comment*/";

            string result = CommentStripper.ProcessSql(sql);

            result.Should().Be("");
        }

        [Test]
        public void MultiLine_ShouldBeRemovedAcrossNewLines()
        {
            string sql = "/*line1\nline2*/";

            string result = CommentStripper.ProcessSql(sql);

            result.Should().Be("");
        }

        [Test]
        public void MultiLine_ExtraStartShouldBeIgnored()
        {
            string sql = "/*abc/*def*/";

            string result = CommentStripper.ProcessSql(sql);

            result.Should().Be("");
        }

        [Test]
        public void MultiLine_ShouldTerminateAtFirstEnd()
        {
            string sql = "/*abc*/def*/";        // this is an SQL error, but fine as far as the comment stripper is concerned

            string result = CommentStripper.ProcessSql(sql);

            result.Should().Be("def*/");
        }

        [Test]
        public void MultiLine_BeforeAndAfterShouldBePreserved()
        {
            string sql = "before/*comment*/after";

            string result = CommentStripper.ProcessSql(sql);

            result.Should().Be("beforeafter");
        }

        [Test]
        public void MultiLine_ExtraStarsShouldBeIgnored()
        {
            string sql = "/***/";

            string result = CommentStripper.ProcessSql(sql);

            result.Should().Be("");
        }

        [Test]
        public void MultiLine_NestedSingleLineCommentsShouldBeIgnored()
        {
            string sql = "a/*--no\n --problem\n*/b";

            string result = CommentStripper.ProcessSql(sql);

            result.Should().Be("ab");
        }

        #endregion

        #region Mixed Comments (The whole shebang!)
        #endregion
    }
}
