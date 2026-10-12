using DbUp;
using DbUp.Engine;
using DbUp.Support;
using Microsoft.Extensions.Logging;

namespace MiniErp.Infrastructure.Persistence;

/// <summary>
/// Applies the SQL scripts from /database (embedded into this assembly):
///  1. migrations/*.sql - run once, in name order, journaled in dbo.SchemaVersions;
///  2. procedures/*.sql - CREATE OR ALTER, re-applied on every run so they always match source control.
/// </summary>
public static class DatabaseMigrator
{
    public static void Migrate(string connectionString, ILogger logger, bool createDatabaseIfMissing)
    {
        if (createDatabaseIfMissing)
            EnsureDatabase.For.SqlDatabase(connectionString);

        var assembly = typeof(DatabaseMigrator).Assembly;
        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(assembly,
                name => name.Contains(".Database.migrations.", StringComparison.OrdinalIgnoreCase),
                new SqlScriptOptions { ScriptType = ScriptType.RunOnce, RunGroupOrder = 1 })
            .WithScriptsEmbeddedInAssembly(assembly,
                name => name.Contains(".Database.procedures.", StringComparison.OrdinalIgnoreCase),
                new SqlScriptOptions { ScriptType = ScriptType.RunAlways, RunGroupOrder = 2 })
            .WithTransactionPerScript()
            .LogToNowhere()
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
            throw new InvalidOperationException($"Database migration failed on script '{result.ErrorScript?.Name}'.", result.Error);

        foreach (var script in result.Scripts)
            logger.LogInformation("Applied database script {Script}", script.Name);
    }
}
