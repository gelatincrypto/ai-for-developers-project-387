using System.Globalization;
using Calendar.Api.Generated.Models;
using Microsoft.Data.Sqlite;

namespace Calendar.Api.EventTypes;

public sealed class BookingStore
{
    private readonly EventTypeStore eventTypes;
    private readonly TimeProvider clock;
    private readonly TimeZoneInfo timeZone;

    public BookingStore(EventTypeStore eventTypes, TimeProvider clock, IConfiguration configuration)
    {
        this.eventTypes = eventTypes;
        this.clock = clock;
        timeZone = TimeZoneInfo.FindSystemTimeZoneById(
            configuration["Owner:TimeZone"] ?? throw new InvalidOperationException("Owner:TimeZone is required."));
        using var connection = eventTypes.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS bookings (
                id TEXT PRIMARY KEY,
                event_type_id TEXT NOT NULL REFERENCES event_types(id),
                start_utc INTEGER NOT NULL,
                end_utc INTEGER NOT NULL CHECK (end_utc > start_utc),
                guest_name TEXT NOT NULL,
                guest_email TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_bookings_start ON bookings (start_utc);
            """;
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<(long Start, long End)> BusyBetween(DateTimeOffset start, DateTimeOffset end)
    {
        using var connection = eventTypes.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT start_utc, end_utc FROM bookings
            WHERE start_utc < $end AND end_utc > $start
            """;
        command.Parameters.AddWithValue("$start", start.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$end", end.ToUnixTimeMilliseconds());
        using var reader = command.ExecuteReader();
        var busy = new List<(long Start, long End)>();
        while (reader.Read())
        {
            busy.Add((reader.GetInt64(0), reader.GetInt64(1)));
        }

        return busy;
    }

    public IReadOnlyList<OwnerMeeting> Upcoming()
    {
        using var connection = eventTypes.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT b.id, b.start_utc, b.end_utc, b.guest_name, b.guest_email,
                   e.id, e.title, e.description, e.duration_minutes
            FROM bookings b JOIN event_types e ON e.id = b.event_type_id
            WHERE b.start_utc > $now
            ORDER BY b.start_utc, b.id
            """;
        command.Parameters.AddWithValue("$now", clock.GetUtcNow().ToUnixTimeMilliseconds());
        using var reader = command.ExecuteReader();
        var result = new List<OwnerMeeting>();
        while (reader.Read())
        {
            result.Add(new OwnerMeeting
            {
                Id = reader.GetString(0),
                Start = ToOwnerTime(reader.GetInt64(1)),
                End = ToOwnerTime(reader.GetInt64(2)),
                GuestName = reader.GetString(3),
                GuestEmail = reader.GetString(4),
                EventType = new EventType
                {
                    Id = reader.GetString(5),
                    Title = reader.GetString(6),
                    Description = reader.GetString(7),
                    DurationMinutes = reader.GetInt32(8),
                },
            });
        }

        return result;
    }

    public string? TryCreate(EventType eventType, DateTimeOffset start, DateTimeOffset end,
        string guestName, string guestEmail, Func<bool> timeStillValid)
    {
        using var connection = eventTypes.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        if (!timeStillValid())
        {
            return null;
        }

        using var conflict = connection.CreateCommand();
        conflict.Transaction = transaction;
        conflict.CommandText = """
            SELECT EXISTS (SELECT 1 FROM bookings
                WHERE start_utc < $end AND end_utc > $start)
            """;
        conflict.Parameters.AddWithValue("$start", start.ToUnixTimeMilliseconds());
        conflict.Parameters.AddWithValue("$end", end.ToUnixTimeMilliseconds());
        if (Convert.ToInt64(conflict.ExecuteScalar()) != 0)
        {
            return null;
        }

        var id = Guid.NewGuid().ToString("N");
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO bookings (id, event_type_id, start_utc, end_utc, guest_name, guest_email)
            VALUES ($id, $eventTypeId, $start, $end, $name, $email)
            """;
        insert.Parameters.AddWithValue("$id", id);
        insert.Parameters.AddWithValue("$eventTypeId", eventType.Id);
        insert.Parameters.AddWithValue("$start", start.ToUnixTimeMilliseconds());
        insert.Parameters.AddWithValue("$end", end.ToUnixTimeMilliseconds());
        insert.Parameters.AddWithValue("$name", guestName);
        insert.Parameters.AddWithValue("$email", guestEmail);
        insert.ExecuteNonQuery();
        transaction.Commit();
        return id;
    }

    private string ToOwnerTime(long milliseconds) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(milliseconds), timeZone)
            .ToString("O", CultureInfo.InvariantCulture);
}
