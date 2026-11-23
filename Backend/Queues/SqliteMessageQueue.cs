using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Backend.Queues;

public sealed class SqliteMessageQueue : IMessageQueue
{
    private readonly string _connectionString;

    public SqliteMessageQueue(string databasePath)
    {
        SqliteConnectionStringBuilder settings = new SqliteConnectionStringBuilder();
        settings.DataSource = databasePath;
        settings.Pooling = false;
        _connectionString = settings.ConnectionString;

        using SqliteConnection connection = new SqliteConnection(_connectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS ReceivedMessages (
                MessageId TEXT PRIMARY KEY NOT NULL,
                Payload TEXT NOT NULL,
                ReceivedUtc TEXT NOT NULL
            );";
        command.ExecuteNonQuery();
    }

    public void Send(Guid messageId, string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        using SqliteConnection connection = new SqliteConnection(_connectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO ReceivedMessages (MessageId, Payload, ReceivedUtc)
            VALUES ($id, $payload, $receivedUtc)
            ON CONFLICT (MessageId) DO NOTHING;";

        command.Parameters.AddWithValue("$id", messageId.ToString());
        command.Parameters.AddWithValue("$payload", message);
        command.Parameters.AddWithValue("$receivedUtc", DateTimeOffset.UtcNow.ToString("O"));

        if (command.ExecuteNonQuery() == 1)
        {
            return;
        }

        command.CommandText = "SELECT Payload FROM ReceivedMessages WHERE MessageId = $id;";
        command.Parameters.Clear();
        command.Parameters.AddWithValue("$id", messageId.ToString());

        string? savedPayload = command.ExecuteScalar() as string;
        if (!string.Equals(savedPayload, message, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Message ID {messageId} already belongs to a different payload.");
        }
    }

    public DateTimeOffset? GetReceivedAt(Guid messageId)
    {
        using SqliteConnection connection = new SqliteConnection(_connectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT ReceivedUtc FROM ReceivedMessages WHERE MessageId = $id;";
        command.Parameters.AddWithValue("$id", messageId.ToString());

        if (command.ExecuteScalar() is string receivedUtc)
        {
            return DateTimeOffset.ParseExact(receivedUtc, "O", CultureInfo.InvariantCulture);
        }

        return null;
    }
}
