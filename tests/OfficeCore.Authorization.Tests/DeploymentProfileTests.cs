using OfficeCore.Desktop.Server;
using Xunit;

namespace OfficeCore.Authorization.Tests;

public class DeploymentProfileTests
{
    [Fact]
    public void Default_database_matches_embedded_profile_without_git_detection()
    {
        Assert.Equal(ServerConfig.GetDefaultDbName(), new ServerConfig().DbName);
        Assert.Contains(ServerConfig.GetDefaultDbName(), new[] { "OfficeCoreDb", "officecoredevdb", "officecoreacbadevdb" });
    }

    [Theory]
    [InlineData("OfficeCoreDb")]
    [InlineData("officecoredevdb")]
    [InlineData("officecoreacbadevdb")]
    public void Reserved_database_can_only_be_used_by_its_own_profile(string database)
    {
        var config = new ServerConfig { DbName = database };
        if (database == ServerConfig.GetDefaultDbName())
            Assert.Equal(database, ReadConnection(config)["database"]);
        else
            Assert.Throws<InvalidOperationException>(() => config.BuildConnectionString());
    }

    [Fact]
    public void Reserved_database_guard_is_case_insensitive()
    {
        var otherDatabase = ServerConfig.GetDefaultDbName() == "OfficeCoreDb" ? "officecoredevdb" : "OfficeCoreDb";
        var config = new ServerConfig { DbName = otherDatabase.ToUpperInvariant() };
        Assert.Throws<InvalidOperationException>(() => config.BuildConnectionString());
    }

    private static System.Data.Common.DbConnectionStringBuilder ReadConnection(ServerConfig config)
        => new() { ConnectionString = config.BuildConnectionString() };

    [Fact]
    public void Connection_values_cannot_override_the_selected_database()
    {
        var config = new ServerConfig { DbPassword = "secret;Database=OfficeCoreDb" };
        var connection = ReadConnection(config);
        Assert.Equal(ServerConfig.GetDefaultDbName(), connection["database"]);
        Assert.Equal(config.DbPassword, connection["password"]);
    }

    [Fact]
    public void Custom_database_selection_is_preserved()
    {
        var config = new ServerConfig { DbName = "customer_custom_database" };
        Assert.Equal("customer_custom_database", ReadConnection(config)["database"]);
        Assert.Equal("customer_custom_database", config.DbName);
    }
}