using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Calendar.Api.Tests;

public class OwnerMeetingsTests
{
    [Fact]
    public async Task OwnerSeesFutureMeetingsAcrossTypes_InOrder_WithGuestContacts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"calendar-meetings-{Guid.NewGuid():N}.db");
        var clock = new MutableClock();
        try
        {
            using (var factory = CreateFactory(path, clock))
            {
                using var owner = factory.CreateClient();
                using var guest = factory.CreateClient();
                Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/owner/bookings")).StatusCode);
                await Login(owner);
                Assert.Empty((await owner.GetFromJsonAsync<Meeting[]>("/api/owner/bookings"))!);

                var shortType = await AddEventType(owner, "Короткий", 30);
                var longType = await AddEventType(owner, "Длинный", 60);
                await Book(guest, shortType, "2026-10-20T10:30:00+03:00", "Ольга", "olga@example.com");
                await Book(guest, longType, "2026-10-21T11:00:00+03:00", "Борис", "boris@example.com");
                await Book(guest, shortType, "2026-10-21T09:00:00+03:00", "Анна", "anna@example.com");

                clock.Now = new DateTimeOffset(2026, 10, 20, 7, 30, 0, TimeSpan.Zero);
                var meetings = await owner.GetFromJsonAsync<Meeting[]>("/api/owner/bookings");
                Assert.NotNull(meetings);
                Assert.Equal(2, meetings.Length);
                Assert.Equal(shortType, meetings[0].EventType.Id);
                Assert.Equal("Анна", meetings[0].GuestName);
                Assert.Equal("anna@example.com", meetings[0].GuestEmail);
                Assert.Equal("2026-10-21T09:00:00.0000000+03:00", meetings[0].Start);
                Assert.Equal("2026-10-21T09:30:00.0000000+03:00", meetings[0].End);
                Assert.Equal(longType, meetings[1].EventType.Id);
                Assert.Equal("Борис", meetings[1].GuestName);
                Assert.Equal("boris@example.com", meetings[1].GuestEmail);
                Assert.Equal("2026-10-21T11:00:00.0000000+03:00", meetings[1].Start);
                Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/owner/bookings")).StatusCode);
                Assert.DoesNotContain("anna@example.com", await (await guest.GetAsync("/api/event-types")).Content.ReadAsStringAsync());
                Assert.DoesNotContain("anna@example.com", await (await guest.GetAsync($"/api/event-types/{shortType}/slots")).Content.ReadAsStringAsync());
            }

            using var restarted = CreateFactory(path, clock);
            using var ownerAfterRestart = restarted.CreateClient();
            await Login(ownerAfterRestart);
            Assert.Equal(2, (await ownerAfterRestart.GetFromJsonAsync<Meeting[]>("/api/owner/bookings"))?.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(string path, TimeProvider clock) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Calendar:DatabasePath", path)
                .UseSetting("Owner:Username", "test-owner")
                .UseSetting("Owner:Password", "test-password")
                .UseSetting("Owner:TimeZone", "Europe/Moscow");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(clock);
            });
        });

    private static async Task Login(HttpClient owner) =>
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync("/api/owner/session",
            new { username = "test-owner", password = "test-password" })).StatusCode);

    private static async Task<string> AddEventType(HttpClient owner, string title, int durationMinutes)
    {
        using var created = await owner.PostAsJsonAsync("/api/event-types",
            new { title, description = "Тест", durationMinutes });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var eventType = await created.Content.ReadFromJsonAsync<EventType>();
        Assert.NotNull(eventType);
        return eventType.Id;
    }

    private static async Task Book(HttpClient guest, string eventTypeId, string start, string guestName, string guestEmail) =>
        Assert.Equal(HttpStatusCode.OK, (await guest.PostAsJsonAsync("/api/bookings",
            new { eventTypeId, start, guestName, guestEmail })).StatusCode);

    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 20, 7, 1, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record EventType(string Id);

    private sealed record Meeting(string Id, EventType EventType, string Start, string End, string GuestName, string GuestEmail);
}
