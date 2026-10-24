using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Calendar.Api.Tests;

public class BookingTests
{
    [Fact]
    public async Task GuestBooking_RejectsOverlapAcrossTypes_ButAllowsTouchingBoundaries()
    {
        var path = NewDatabasePath();
        string hour;
        try
        {
            using (var factory = CreateFactory(path))
            {
                using var owner = factory.CreateClient();
                using var guest = factory.CreateClient();
                hour = await CreateType(owner, "Час", 60);
                var halfHour = await CreateType(owner, "Полчаса", 30);

                var first = await guest.PostAsJsonAsync("/api/bookings", Request(hour, "2026-10-21T09:00:00+03:00"));
                Assert.Equal(HttpStatusCode.OK, first.StatusCode);
                using var confirmation = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
                Assert.False(string.IsNullOrWhiteSpace(confirmation.RootElement.GetProperty("id").GetString()));
                Assert.Equal(hour, confirmation.RootElement.GetProperty("eventType").GetProperty("id").GetString());
                Assert.Equal("2026-10-21T09:00:00.0000000+03:00", confirmation.RootElement.GetProperty("start").GetString());
                Assert.Equal("2026-10-21T10:00:00.0000000+03:00", confirmation.RootElement.GetProperty("end").GetString());
                Assert.DoesNotContain("guest@example.com", await first.Content.ReadAsStringAsync());

                Assert.Equal(HttpStatusCode.Conflict,
                    (await guest.PostAsJsonAsync("/api/bookings", Request(halfHour, "2026-10-21T09:30:00+03:00"))).StatusCode);
                Assert.Equal(HttpStatusCode.OK,
                    (await guest.PostAsJsonAsync("/api/bookings", Request(halfHour, "2026-10-21T10:00:00+03:00"))).StatusCode);

                using var slots = await guest.GetAsync($"/api/event-types/{halfHour}/slots");
                var calendar = await slots.Content.ReadFromJsonAsync<Calendar>();
                Assert.NotNull(calendar);
                Assert.DoesNotContain(calendar.Slots, slot => slot.Start.StartsWith("2026-10-21T09:", StringComparison.Ordinal));
                Assert.DoesNotContain(calendar.Slots, slot => slot.Start.StartsWith("2026-10-21T10:00", StringComparison.Ordinal));
                Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/api/event-types")).StatusCode);
            }

            using var restarted = CreateFactory(path);
            using var visitor = restarted.CreateClient();
            Assert.Equal(HttpStatusCode.Conflict,
                (await visitor.PostAsJsonAsync("/api/bookings", Request(hour, "2026-10-21T09:00:00+03:00"))).StatusCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DirectRequests_CannotBypassScheduleOrValidation()
    {
        var path = NewDatabasePath();
        try
        {
            using var factory = CreateFactory(path);
            using var owner = factory.CreateClient();
            using var guest = factory.CreateClient();
            var hour = await CreateType(owner, "Час", 60);

            foreach (var time in new[]
            {
                "2026-10-20T09:00:00+03:00", // passed today
                "2026-10-21T08:30:00+03:00", // before work
                "2026-10-21T17:30:00+03:00", // end beyond 18:00
                "2026-11-03T09:00:00+03:00", // fifteenth date
                "2026-10-21T09:15:00+03:00", // off grid
            })
            {
                Assert.Equal(HttpStatusCode.Conflict,
                    (await guest.PostAsJsonAsync("/api/bookings", Request(hour, time))).StatusCode);
            }

            foreach (var weekend in new[] { "2026-10-24T09:00:00+03:00", "2026-10-25T09:00:00+03:00" })
            {
                Assert.Equal(HttpStatusCode.OK,
                    (await guest.PostAsJsonAsync("/api/bookings", Request(hour, weekend))).StatusCode);
                Assert.Equal(HttpStatusCode.Conflict,
                    (await guest.PostAsJsonAsync("/api/bookings", Request(hour, weekend))).StatusCode);
            }

            Assert.Equal(HttpStatusCode.BadRequest,
                (await guest.PostAsJsonAsync("/api/bookings", Request(hour, "2026-10-21T09:00:00"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await guest.PostAsJsonAsync("/api/bookings", new
                {
                    eventTypeId = hour,
                    start = "2026-10-21T09:00:00+03:00",
                    guestName = " ",
                    guestEmail = "not-an-email",
                })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await guest.PostAsJsonAsync("/api/bookings", Request("missing", "2026-10-21T09:00:00+03:00"))).StatusCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ParallelGuests_CannotBookTheSameInterval()
    {
        var path = NewDatabasePath();
        try
        {
            using var factory = CreateFactory(path);
            using var owner = factory.CreateClient();
            var hour = await CreateType(owner, "Час", 60);
            using var guest1 = factory.CreateClient();
            using var guest2 = factory.CreateClient();
            var time = "2026-10-21T11:00:00+03:00";

            var results = await Task.WhenAll(
                guest1.PostAsJsonAsync("/api/bookings", Request(hour, time)),
                guest2.PostAsJsonAsync("/api/bookings", Request(hour, time)));
            Assert.Equal(1, results.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, results.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static object Request(string id, string start) =>
        new { eventTypeId = id, start, guestName = "Гость", guestEmail = "guest@example.com" };

    private static string NewDatabasePath() => Path.Combine(Path.GetTempPath(), $"calendar-booking-{Guid.NewGuid():N}.db");

    private static WebApplicationFactory<Program> CreateFactory(string path) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Calendar:DatabasePath", path)
                .UseSetting("Owner:Username", "test-owner")
                .UseSetting("Owner:Password", "test-password")
                .UseSetting("Owner:TimeZone", "Europe/Moscow");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FrozenClock());
            });
        });

    private static async Task<string> CreateType(HttpClient owner, string title, int minutes)
    {
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync("/api/owner/session",
            new { username = "test-owner", password = "test-password" })).StatusCode);
        using var created = await owner.PostAsJsonAsync("/api/event-types",
            new { title, description = "Звонок", durationMinutes = minutes });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var eventType = await created.Content.ReadFromJsonAsync<EventType>();
        Assert.NotNull(eventType);
        return eventType.Id;
    }

    private sealed class FrozenClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 20, 7, 1, 0, TimeSpan.Zero);
    }

    private sealed record Calendar(Slot[] Slots);

    private sealed record EventType(string Id);

    private sealed record Slot(string Start);
}
