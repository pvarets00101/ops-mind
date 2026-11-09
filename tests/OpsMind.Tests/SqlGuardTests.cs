using OpsMind.Core.Agent;

namespace OpsMind.Tests;

public class SqlGuardTests {
    [Theory]
    [InlineData("SELECT count(*) FROM tickets")]
    [InlineData("select team, count(*) from tickets group by team;")]
    [InlineData("WITH t AS (SELECT * FROM tickets) SELECT count(*) FROM t")]
    public void Check_AllowsSelect_AndWrapsWithLimit(string sql) {
        var check = SqlGuard.Check(sql, 30);

        Assert.True(check.IsValid, check.Error);
        Assert.EndsWith("LIMIT 30", check.Sql);
        Assert.DoesNotContain(";", check.Sql);
    }

    [Theory]
    [InlineData("DELETE FROM tickets")]
    [InlineData("SELECT 1; DROP TABLE tickets")]
    [InlineData("UPDATE tickets SET status = 'resolved'")]
    [InlineData("WITH x AS (DELETE FROM tickets RETURNING *) SELECT * FROM x")]
    [InlineData("SELECT pg_sleep(100)")]
    [InlineData("SELECT pg_read_file('/etc/passwd')")]
    [InlineData("")]
    public void Check_RejectsDangerousOrInvalid(string sql) {
        var check = SqlGuard.Check(sql, 30);

        Assert.False(check.IsValid);
        Assert.NotNull(check.Error);
    }
}