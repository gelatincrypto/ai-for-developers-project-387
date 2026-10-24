using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Calendar.Api.Tests;

public class SlotTests
{
    [Fact]
    public async Task WindowStartsOnOwnerDate_WhenUtcDateDiffers()
    {
        var path = Path.Combine(Path.GetTempPath(), $"calendar-slots-{Guid.NewGuid():N}.db");
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Calendar:DatabasePath", path)
                    .UseSetting("Owner:Username", "test-owner")
                    .UseSetting("Owner:Password", "test-password")
                    .UseSetting("Owner:TimeZone", "Europe/Moscow");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new FrozenClock(new DateTimeOffset(2026, 10, 19, 22, 0, 0, TimeSpan.Zero)));
                });
            });
            using var owner = factory.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync("/api/owner/session",
                new { username = "test-owner", password = "test-password" })).StatusCode);
            using var created = await owner.PostAsJsonAsync("/api/event-types",
                new { title = "Звонок", description = "Тест", durationMinutes = 30 });
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            var eventType = await created.Content.ReadFromJsonAsync<EventType>();
            Assert.NotNull(eventType);

            using var guest = factory.CreateClient();
            var calendar = await guest.GetFromJsonAsync<Calendar>($"/api/event-types/{eventType.Id}/slots");
            Assert.NotNull(calendar);
            Assert.Equal("2026-10-20", calendar.WindowStartDate);
            Assert.Equal(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(3)), calendar.Slots[0].Start);
            Assert.Contains(calendar.Slots, slot => slot.Start == new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.FromHours(3)));
            Assert.Contains(calendar.Slots, slot => slot.Start == new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.FromHours(3)));
            Assert.Equal(new DateTimeOffset(2026, 11, 2, 17, 30, 0, TimeSpan.FromHours(3)), calendar.Slots[^1].Start);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Slots_IncludeTodayAndLastDay_WithinWorkingHours()
    {
        var path = Path.Combine(Path.GetTempPath(), $"calendar-slots-{Guid.NewGuid():N}.db");
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Calendar:DatabasePath", path)
                    .UseSetting("Owner:Username", "test-owner")
                    .UseSetting("Owner:Password", "test-password")
                    .UseSetting("Owner:TimeZone", "Europe/Moscow");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new FrozenClock(new DateTimeOffset(2026, 10, 20, 7, 1, 0, TimeSpan.Zero)));
                });
            });
            using var guest = factory.CreateClient();
            using var owner = factory.CreateClient();

            Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync("/api/event-types/missing/slots")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync("/api/owner/session",
                new { username = "test-owner", password = "test-password" })).StatusCode);
            using var created = await owner.PostAsJsonAsync("/api/event-types",
                new { title = "Звонок", description = "Тест", durationMinutes = 75 });
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            var eventType = await created.Content.ReadFromJsonAsync<EventType>();
            Assert.NotNull(eventType);
            using var longCreated = await owner.PostAsJsonAsync("/api/event-types",
                new { title = "Длинный", description = "Тест", durationMinutes = 541 });
            Assert.Equal(HttpStatusCode.OK, longCreated.StatusCode);
            var longType = await longCreated.Content.ReadFromJsonAsync<EventType>();
            Assert.NotNull(longType);

            var calendar = await guest.GetFromJsonAsync<Calendar>($"/api/event-types/{eventType.Id}/slots");
            Assert.NotNull(calendar);
            Assert.Equal("Europe/Moscow", calendar.OwnerTimeZone);
            Assert.NotEmpty(calendar.Slots);
            Assert.Equal(new DateTimeOffset(2026, 10, 20, 10, 30, 0, TimeSpan.FromHours(3)), calendar.Slots[0].Start);
            Assert.Equal(new DateTimeOffset(2026, 10, 20, 11, 45, 0, TimeSpan.FromHours(3)), calendar.Slots[0].End);
            Assert.Equal(new DateTimeOffset(2026, 11, 2, 16, 30, 0, TimeSpan.FromHours(3)), calendar.Slots[^1].Start);
            Assert.All(calendar.Slots, slot =>
            {
                Assert.InRange(slot.Start.Hour, 9, 17);
                Assert.Equal(0, slot.Start.Minute % 30);
                Assert.Equal(TimeSpan.FromMinutes(75), slot.End - slot.Start);
                Assert.True(slot.End.Hour < 18 || slot.End is { Hour: 18, Minute: 0 });
                Assert.InRange(DateOnly.FromDateTime(slot.Start.DateTime), new DateOnly(2026, 10, 20), new DateOnly(2026, 11, 2));
            });
            Assert.Contains(calendar.Slots, slot => slot.Start.DayOfWeek == DayOfWeek.Saturday);
            Assert.Contains(calendar.Slots, slot => slot.Start.DayOfWeek == DayOfWeek.Sunday);

            var empty = await guest.GetFromJsonAsync<Calendar>($"/api/event-types/{longType.Id}/slots");
            Assert.NotNull(empty);
            Assert.Equal("2026-10-20", empty.WindowStartDate);
            Assert.Empty(empty.Slots);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class FrozenClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record EventType(string Id);

    private sealed record Calendar(string OwnerTimeZone, string? WindowStartDate, Slot[] Slots);

    private sealed record Slot(DateTimeOffset Start, DateTimeOffset End);
}
