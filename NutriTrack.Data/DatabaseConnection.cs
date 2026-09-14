using MySqlConnector;

namespace NutriTrack.Data;

/// <summary>
/// Central place that knows how to open a connection to the NutriTrack MySQL database.
/// Repositories (ProductRepository, ReceiptRepository, ...) take this in their constructor.
/// </summary>
public class DatabaseConnection
{
    private readonly string _connectionString;

    public DatabaseConnection(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Convenience factory for local development.
    /// TODO: move these values into an appsettings.json / user secrets before submitting.
    /// </summary>
    public static DatabaseConnection CreateLocalDefault()
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = "localhost",
            Port = 3306,
            Database = "nutritrack",
            UserID = "root",
            Password = "changeme"
        };
        return new DatabaseConnection(builder.ConnectionString);
    }

    public MySqlConnection CreateOpenConnection()
    {
        var connection = new MySqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>Quick check used on app startup to confirm the DB is reachable.</summary>
    public bool CanConnect()
    {
        try
        {
            using var connection = CreateOpenConnection();
            return connection.State == System.Data.ConnectionState.Open;
        }
        catch
        {
            return false;
        }
    }
}
