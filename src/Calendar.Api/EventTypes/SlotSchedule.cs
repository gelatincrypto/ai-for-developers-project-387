using System.Globalization;
using Calendar.Api.Generated.Models;

namespace Calendar.Api.EventTypes;

public sealed class SlotSchedule(IConfiguration configuration, TimeProvider clock, BookingStore bookings)
{
    private readonly TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(
        configuration["Owner:TimeZone"] ?? throw new InvalidOperationException("Owner:TimeZone is required."));

    public AvailableSlots GetSlots(EventType eventType, bool excludeBusy = true)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
        var slots = new List<AvailableSlot>();

        for (var day = 0; day < 14; day++)
        {
            var date = today.AddDays(day);
            for (var minute = 9 * 60; minute + eventType.DurationMinutes <= 18 * 60; minute += 30)
            {
                var localStart = date.ToDateTime(new TimeOnly(minute / 60, minute % 60));
                if (timeZone.IsInvalidTime(localStart))
                {
                    continue;
                }

                var start = new DateTimeOffset(localStart, timeZone.GetUtcOffset(localStart));
                var end = TimeZoneInfo.ConvertTime(start.AddMinutes(eventType.DurationMinutes), timeZone);
                if (start <= now || DateOnly.FromDateTime(end.DateTime) != date ||
                    end.TimeOfDay > TimeSpan.FromHours(18))
                {
                    continue;
                }

                slots.Add(new AvailableSlot
                {
                    Start = start.ToString("O", CultureInfo.InvariantCulture),
                    End = end.ToString("O", CultureInfo.InvariantCulture),
                });
            }
        }

        if (excludeBusy && slots.Count > 0)
        {
            var busy = bookings.BusyBetween(now, now.AddDays(15));
            slots.RemoveAll(slot =>
            {
                var start = DateTimeOffset.Parse(slot.Start, CultureInfo.InvariantCulture).ToUnixTimeMilliseconds();
                var end = DateTimeOffset.Parse(slot.End, CultureInfo.InvariantCulture).ToUnixTimeMilliseconds();
                return busy.Any(booking => booking.Start < end && booking.End > start);
            });
        }

        return new AvailableSlots
        {
            OwnerTimeZone = timeZone.Id,
            WindowStartDate = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Slots = slots,
        };
    }
}
