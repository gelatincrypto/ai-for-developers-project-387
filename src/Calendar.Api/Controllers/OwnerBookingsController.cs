using Calendar.Api.EventTypes;
using Calendar.Api.Generated.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Calendar.Api.Controllers;

[Authorize]
public sealed class OwnerBookingsController(BookingStore bookings) : OwnerBookingsApiController
{
    public override IActionResult ListOwnerBookings() => Ok(bookings.Upcoming());
}
