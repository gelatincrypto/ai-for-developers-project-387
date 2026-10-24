using Calendar.Api.Generated.Models;
using Microsoft.Data.Sqlite;

namespace Calendar.Api.EventTypes;

public sealed class EventTypeStore
{
    private readonly string connectionString;

    public EventTypeStore(IConfiguration configuration, IHostEnvironment environment)
    {
        var path = configuration["Calendar:DatabasePath"] ?? Path.Combine(environment.ContentRootPath, "calendar.db");
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS event_types (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                description TEXT NOT NULL,
                duration_minutes INTEGER NOT NULL CHECK (duration_minutes > 0)
            );
            """;
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<EventType> List()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, title, description, duration_minutes FROM event_types ORDER BY id";
        using var reader = command.ExecuteReader();
        var result = new List<EventType>();
        while (reader.Read())
        {
            result.Add(new EventType
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Description = reader.GetString(2),
                DurationMinutes = reader.GetInt32(3),
            });
        }

        return result;
    }

    public EventType? Find(string id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, title, description, duration_minutes FROM event_types WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? new EventType
        {
            Id = reader.GetString(0),
            Title = reader.GetString(1),
            Description = reader.GetString(2),
            DurationMinutes = reader.GetInt32(3),
        } : null;
    }

    public EventType Create(EventTypeInput input)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO event_types (id, title, description, duration_minutes)
            VALUES ($id, $title, $description, $duration)
            """;
        var id = command.Parameters.Add("$id", SqliteType.Text);
        var title = input.Title.Trim();
        var description = input.Description.Trim();
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$description", description);
        command.Parameters.AddWithValue("$duration", input.DurationMinutes);
        string generatedId;
        do
        {
            generatedId = Guid.NewGuid().ToString("N");
            id.Value = generatedId;
        } while (command.ExecuteNonQuery() == 0);

        return new EventType
        {
            Id = generatedId,
            Title = title,
            Description = description,
            DurationMinutes = input.DurationMinutes,
        };
    }

    internal SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}
