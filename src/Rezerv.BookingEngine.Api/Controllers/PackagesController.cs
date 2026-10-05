using Microsoft.AspNetCore.Mvc;
using Rezerv.BookingEngine.Application;

namespace Rezerv.BookingEngine.Api.Controllers;

[ApiController]
[Route("api/packages")]
public sealed class PackagesController(IBookingEngine bookingEngine) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PackageDefinitionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PackageDefinitionResponse>>> Get(
        [FromQuery] int? businessId, CancellationToken cancellationToken) =>
        Ok(await bookingEngine.GetPackagesAsync(businessId, cancellationToken));

    [HttpPost("purchase")]
    [ProducesResponseType<CustomerPackageResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomerPackageResponse>> Purchase(
        PurchasePackageRequest request, CancellationToken cancellationToken)
    {
        var result = await bookingEngine.PurchasePackageAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}

