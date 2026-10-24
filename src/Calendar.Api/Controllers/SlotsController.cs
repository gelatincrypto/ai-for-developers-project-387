using Calendar.Api.EventTypes;
using Calendar.Api.Generated.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Calendar.Api.Controllers;

[AllowAnonymous]
public sealed class SlotsController(EventTypeStore store, SlotSchedule schedule) : SlotsApiController
{
    public override IActionResult ListSlots(string eventTypeId)
    {
        var eventType = store.Find(eventTypeId);
        return eventType is null ? NotFound() : Ok(schedule.GetSlots(eventType));
    }
}
