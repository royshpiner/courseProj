using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Backend.Persistence
{
    public sealed class SqliteOutbox
    {
        private readonly string _connectionString;

        public SqliteOutbox(string databasePath)
        {
            SqliteConnectionStringBuilder settings = new SqliteConnectionStringBuilder();
            settings.DataSource = databasePath;
            settings.Pooling = false;
            _connectionString = settings.ConnectionString;
            EnsureOutboxTableExists();
        }

        public void SavePending(Guid messageId, string message)
        {
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT INTO Outbox (MessageId, Payload, MessageLength, CreatedUtc)
                        VALUES ($id, $payload, $length, $createdUtc);";

                    command.Parameters.AddWithValue("$id", messageId.ToString());
                    command.Parameters.AddWithValue("$payload", message);
                    command.Parameters.AddWithValue("$length", message.Length);
                    command.Parameters.AddWithValue("$createdUtc", DateTimeOffset.UtcNow.ToString("O"));

                    command.ExecuteNonQuery();
                }
            }
        }

        public bool TryMarkSent(Guid messageId, DateTimeOffset sentAt)
        {
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        UPDATE Outbox
                        SET SentUtc = $sentUtc
                        WHERE MessageId = $id AND SentUtc IS NULL;";

                    command.Parameters.AddWithValue("$id", messageId.ToString());
                    command.Parameters.AddWithValue("$sentUtc", sentAt.ToUniversalTime().ToString("O"));

                    int updatedRows = command.ExecuteNonQuery();
                    return updatedRows == 1;
                }
            }
        }

        public DateTimeOffset? GetSentAt(Guid messageId)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT SentUtc FROM Outbox WHERE MessageId = $id;";
            command.Parameters.AddWithValue("$id", messageId.ToString());

            if (command.ExecuteScalar() is string sentUtc)
            {
                return DateTimeOffset.ParseExact(sentUtc, "O", CultureInfo.InvariantCulture);
            }

            return null;
        }

        private void EnsureOutboxTableExists()
        {
            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Outbox (
                            SequenceNumber INTEGER PRIMARY KEY AUTOINCREMENT,
                            MessageId TEXT NOT NULL UNIQUE,
                            Payload TEXT NOT NULL,
                            MessageLength INTEGER NOT NULL,
                            CreatedUtc TEXT NOT NULL,
                            SentUtc TEXT
                        );";
                    command.ExecuteNonQuery();
                }
            }
        }

        public IReadOnlyList<Guid> GetUnconfirmedMessageIds()
        {
            List<Guid> messageIds = new List<Guid>();

            using (SqliteConnection connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT MessageId
                        FROM Outbox
                        WHERE SentUtc IS NULL
                        ORDER BY SequenceNumber;";

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            messageIds.Add(Guid.Parse(reader.GetString(0)));
                        }
                    }
                }
            }

            return messageIds;
        }

        public IReadOnlyList<(Guid MessageId, string Payload)> GetUnconfirmedMessages()
        {
            List<(Guid MessageId, string Payload)> messages = new List<(Guid MessageId, string Payload)>();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                SELECT MessageId, Payload
                FROM Outbox
                WHERE SentUtc IS NULL
                ORDER BY SequenceNumber;";

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                messages.Add((Guid.Parse(reader.GetString(0)), reader.GetString(1)));
            }

            return messages;
        }
    }
}
