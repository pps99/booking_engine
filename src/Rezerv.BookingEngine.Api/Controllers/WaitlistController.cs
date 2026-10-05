using Microsoft.AspNetCore.Mvc;
using Rezerv.BookingEngine.Application;

namespace Rezerv.BookingEngine.Api.Controllers;

[ApiController]
[Route("api/waitlist")]
public sealed class WaitlistController(IBookingEngine bookingEngine) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BookingResponse>> Join(
        JoinWaitlistRequest request, CancellationToken cancellationToken) =>
        StatusCode(
            StatusCodes.Status201Created,
            await bookingEngine.JoinWaitlistAsync(request, cancellationToken));
}

