using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Calendar.Api.EventTypes;
using Calendar.Api.Generated.Controllers;
using Calendar.Api.Generated.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Calendar.Api.Controllers;

[AllowAnonymous]
public sealed class BookingsController(EventTypeStore eventTypes, BookingStore bookings, SlotSchedule schedule)
    : BookingsApiController
{
    public override IActionResult CreateBooking(BookingRequest bookingRequest)
    {
        if (string.IsNullOrWhiteSpace(bookingRequest.GuestName) ||
            string.IsNullOrWhiteSpace(bookingRequest.GuestEmail) ||
            !MailAddress.TryCreate(bookingRequest.GuestEmail, out var email) ||
            email.Address != bookingRequest.GuestEmail ||
            !Regex.IsMatch(bookingRequest.Start, @"(?:Z|[+-]\d{2}:\d{2})$") ||
            !DateTimeOffset.TryParse(bookingRequest.Start, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var requestedStart))
        {
            return BadRequest(new { error = "Укажите имя, корректный email и время со смещением." });
        }

        var eventType = eventTypes.Find(bookingRequest.EventTypeId);
        if (eventType is null)
        {
            return NotFound(new { error = "Тип события не найден." });
        }

        var slot = schedule.GetSlots(eventType).Slots.FirstOrDefault(candidate =>
            DateTimeOffset.Parse(candidate.Start, CultureInfo.InvariantCulture) == requestedStart);
        if (slot is null)
        {
            return Conflict(new { error = "Время недоступно. Обновите календарь и выберите другой слот." });
        }

        var start = DateTimeOffset.Parse(slot.Start, CultureInfo.InvariantCulture);
        var end = DateTimeOffset.Parse(slot.End, CultureInfo.InvariantCulture);
        var id = bookings.TryCreate(eventType, start, end,
            bookingRequest.GuestName.Trim(), bookingRequest.GuestEmail,
            () => schedule.GetSlots(eventType, excludeBusy: false).Slots.Any(candidate => candidate.Start == slot.Start));
        if (id is null)
        {
            return Conflict(new { error = "Время занято. Обновите календарь и выберите другой слот." });
        }

        return Ok(new BookingConfirmation
        {
            Id = id,
            EventType = eventType,
            Start = slot.Start,
            End = slot.End,
        });
    }
}
