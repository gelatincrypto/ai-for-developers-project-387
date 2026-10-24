using Calendar.Api.EventTypes;
using Calendar.Api.Generated.Controllers;
using Calendar.Api.Generated.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Calendar.Api.Controllers;

public sealed class EventTypesController(EventTypeStore store) : EventTypesApiController
{
    public override IActionResult CreateEventType(EventTypeInput eventTypeInput)
    {
        if (string.IsNullOrWhiteSpace(eventTypeInput.Title) ||
            string.IsNullOrWhiteSpace(eventTypeInput.Description) ||
            eventTypeInput.DurationMinutes <= 0)
        {
            return BadRequest(new { error = "Заполните название, описание и положительную длительность." });
        }

        return Ok(store.Create(eventTypeInput));
    }

    [AllowAnonymous]
    public override IActionResult ListEventTypes() => Ok(store.List());
}
