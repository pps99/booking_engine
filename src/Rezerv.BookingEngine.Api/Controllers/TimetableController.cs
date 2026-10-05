using Microsoft.AspNetCore.Mvc;
using Rezerv.BookingEngine.Application;

namespace Rezerv.BookingEngine.Api.Controllers;

[ApiController]
[Route("api/timetable")]
public sealed class TimetableController(IBookingEngine bookingEngine) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TimetableResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TimetableResponse>>> Get(
        [FromQuery] int? businessId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken) =>
        Ok(await bookingEngine.GetTimetableAsync(businessId, date, cancellationToken));
}

