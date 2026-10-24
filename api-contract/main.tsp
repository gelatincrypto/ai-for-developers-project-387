import "@typespec/http";
import "@typespec/openapi";

using Http;
using OpenAPI;

@service(#{ title: "Календарь звонков" })
@info(#{ version: "1.0.0" })
namespace CalendarApi {
  model HealthResponse {
    status: "ok";
  }

  @doc("Проверить доступность API.")
  @tag("Health")
  @get
  @route("/api/health")
  op getHealth(): HealthResponse;

  model OwnerCredentials {
    username: string;
    password: string;
  }

  model OwnerSession {
    username: string;
  }

  @tag("Owner")
  @post
  @route("/api/owner/session")
  op login(@body credentials: OwnerCredentials): OwnerSession;

  @tag("Owner")
  @get
  @route("/api/owner/session")
  op getOwnerSession(): OwnerSession;

  @tag("Owner")
  @delete
  @route("/api/owner/session")
  op logout(): void;

  model EventTypeInput {
    title: string;
    description: string;
    @minValue(1)
    durationMinutes: int32;
  }

  model EventType {
    id: string;
    title: string;
    description: string;
    durationMinutes: int32;
  }

  @tag("EventTypes")
  @get
  @route("/api/event-types")
  op listEventTypes(): EventType[];

  @tag("EventTypes")
  @post
  @route("/api/event-types")
  op createEventType(@body eventType: EventTypeInput): EventType;

  model AvailableSlot {
    start: string;
    end: string;
  }

  model AvailableSlots {
    ownerTimeZone: string;
    windowStartDate: string;
    slots: AvailableSlot[];
  }

  @tag("Slots")
  @get
  @route("/api/event-types/{eventTypeId}/slots")
  op listSlots(@path eventTypeId: string): AvailableSlots;

  model BookingRequest {
    eventTypeId: string;
    start: string;
    guestName: string;
    guestEmail: string;
  }

  model BookingConfirmation {
    id: string;
    eventType: EventType;
    start: string;
    end: string;
  }

  @tag("Bookings")
  @post
  @route("/api/bookings")
  op createBooking(@body booking: BookingRequest): BookingConfirmation;

  model OwnerMeeting {
    id: string;
    eventType: EventType;
    start: string;
    end: string;
    guestName: string;
    guestEmail: string;
  }

  @tag("OwnerBookings")
  @get
  @route("/api/owner/bookings")
  op listOwnerBookings(): OwnerMeeting[];
}
