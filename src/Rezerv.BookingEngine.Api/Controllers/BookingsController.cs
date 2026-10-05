using Microsoft.AspNetCore.Mvc;
using Rezerv.BookingEngine.Application;

namespace Rezerv.BookingEngine.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController(IBookingEngine bookingEngine) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BookingResponse>> Book(
        BookClassRequest request, CancellationToken cancellationToken)
    {
        var result = await bookingEngine.BookAsync(request, cancellationToken);
        return result.Status == "booked"
            ? StatusCode(StatusCodes.Status201Created, result)
            : Ok(result);
    }

    [HttpPost("cancel")]
    [ProducesResponseType<CancellationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CancellationResponse>> Cancel(
        CancelBookingRequest request, CancellationToken cancellationToken) =>
        Ok(await bookingEngine.CancelAsync(request, cancellationToken));
}

