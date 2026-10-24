using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Calendar.Api.Tests;

public class EventTypeTests
{
    [Fact]
    public async Task OwnerCreatesType_AndGuestReadsCatalog_AfterRestart()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"calendar-test-{Guid.NewGuid():N}.db");
        string firstId;
        string secondId;
        try
        {
            using (var factory = CreateFactory(databasePath))
            {
                using var owner = factory.CreateClient();
                using var guest = factory.CreateClient();
                var input = new { title = "Консультация", description = "Знакомство", durationMinutes = 45 };

                Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/event-types", input)).StatusCode);
                Assert.Empty((await guest.GetFromJsonAsync<EventType[]>("/api/event-types"))!);
                Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync("/api/owner/session",
                    new { username = "test-owner", password = "test-password" })).StatusCode);

                using var created = await owner.PostAsJsonAsync("/api/event-types", input);
                Assert.Equal(HttpStatusCode.OK, created.StatusCode);
                var first = await created.Content.ReadFromJsonAsync<EventType>();
                Assert.NotNull(first);
                Assert.False(string.IsNullOrWhiteSpace(first.Id));
                Assert.Equal(new EventType(first.Id, "Консультация", "Знакомство", 45), first);
                using var secondResponse = await owner.PostAsJsonAsync("/api/event-types",
                    new { id = "client-chosen", input.title, input.description, input.durationMinutes });
                Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
                var second = await secondResponse.Content.ReadFromJsonAsync<EventType>();
                Assert.NotNull(second);
                Assert.NotEqual(first.Id, second.Id);
                Assert.NotEqual("client-chosen", second.Id);
                firstId = first.Id;
                secondId = second.Id;
                Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/event-types",
                    new { title = " ", description = "Знакомство", durationMinutes = 30 })).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/event-types",
                    new { title = "Тест", description = "Тест", durationMinutes = 0 })).StatusCode);
                var catalog = await guest.GetFromJsonAsync<EventType[]>("/api/event-types");
                Assert.NotNull(catalog);
                Assert.Equal(2, catalog.Length);
                Assert.Contains(first, catalog);
                Assert.Contains(second, catalog);
            }

            using var restarted = CreateFactory(databasePath);
            using var visitor = restarted.CreateClient();
            var afterRestart = await visitor.GetFromJsonAsync<EventType[]>("/api/event-types");
            Assert.NotNull(afterRestart);
            Assert.Equal(2, afterRestart.Length);
            Assert.Contains(afterRestart, item => item.Id == firstId);
            Assert.Contains(afterRestart, item => item.Id == secondId);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ExistingType_KeepsItsLinkAfterRestart()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"calendar-test-{Guid.NewGuid():N}.db");
        try
        {
            using (var factory = CreateFactory(databasePath))
            using (var guest = factory.CreateClient())
            {
                Assert.Empty((await guest.GetFromJsonAsync<EventType[]>("/api/event-types"))!);
            }

            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Pooling = false,
            }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO event_types (id, title, description, duration_minutes) VALUES ('consultation', 'Консультация', 'Знакомство', 45)";
                command.ExecuteNonQuery();
            }

            using (var restarted = CreateFactory(databasePath))
            using (var visitor = restarted.CreateClient())
            {
                Assert.Contains(new EventType("consultation", "Консультация", "Знакомство", 45),
                    (await visitor.GetFromJsonAsync<EventType[]>("/api/event-types"))!);
                Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync("/api/event-types/consultation/slots")).StatusCode);
            }
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(string databasePath) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("Calendar:DatabasePath", databasePath)
                .UseSetting("Owner:Username", "test-owner")
                .UseSetting("Owner:Password", "test-password"));

    private sealed record EventType(string Id, string Title, string Description, int DurationMinutes);
}
