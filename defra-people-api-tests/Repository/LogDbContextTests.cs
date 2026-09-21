using System;
using System.Linq;
using Defra_People_API.Entities;
using Defra_People_API.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace defra.people_api_tests.Repository;

public class LogDbContextTests
{
    [Fact]
    public void ApiLogs_DbSet_IsInitialized()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<LogDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDatabase_" + Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new LogDbContext(options);

        // Assert
        Assert.NotNull(context.ApiLogs);
    }

    [Fact]
    public void OnModelCreating_ConfiguresLogItemEntity_Correctly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<LogDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDatabase_" + Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new LogDbContext(options);
        var model = context.Model;
        var entityType = model.FindEntityType(typeof(LogItem));
        var tableName = entityType?.GetTableName();
        var primaryKey = entityType?.FindPrimaryKey();
        var requiredProperties = new[] { "Consumer", "Endpoint", "User", "Params", "Response", "Timestamp" };

        // Assert
        Assert.Equal("PeopleServiceLog", tableName);
        Assert.NotNull(primaryKey);
        Assert.Equal("Id", primaryKey.Properties.Single().Name);

        foreach (var propertyName in requiredProperties)
        {
            var property = entityType?.FindProperty(propertyName);
            Assert.NotNull(property);
            Assert.False(property.IsNullable);
        }
    }

    [Fact]
    public void LogDbContext_CanAddAndRetrieveLogItems()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<LogDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDatabase_" + Guid.NewGuid().ToString())
            .Options;

        var logItem = new LogItem
        {
            Consumer = "TestConsumer",
            Method = "GET",
            Endpoint = "/api/test",
            User = "TestUser",
            Params = "{}",
            Response = "{}",
            LogLevel = LogLevel.Information,
            Timestamp = DateTime.UtcNow,
            Runtime = TimeSpan.FromMilliseconds(100)
        };

        // Act - Add the log item
        using (var context = new LogDbContext(options))
        {
            context.ApiLogs.Add(logItem);
            context.SaveChanges();
        }

        // Act - Retrieve the log item
        LogItem retrievedItem;
        using (var context = new LogDbContext(options))
        {
            retrievedItem = context.ApiLogs.Single();
        }

        // Assert
        Assert.Equal(logItem.Id, retrievedItem.Id);
        Assert.Equal(logItem.Consumer, retrievedItem.Consumer);
        Assert.Equal(logItem.Method, retrievedItem.Method);
        Assert.Equal(logItem.Endpoint, retrievedItem.Endpoint);
        Assert.Equal(logItem.User, retrievedItem.User);
        Assert.Equal(logItem.Params, retrievedItem.Params);
        Assert.Equal(logItem.Response, retrievedItem.Response);
        Assert.Equal(logItem.LogLevel, retrievedItem.LogLevel);
        Assert.Equal(logItem.Timestamp, retrievedItem.Timestamp);
        Assert.Equal(logItem.Runtime, retrievedItem.Runtime);
    }
}
