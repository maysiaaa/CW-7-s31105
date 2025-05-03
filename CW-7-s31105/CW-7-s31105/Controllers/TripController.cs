using CW_7_s31105.Services;
using Microsoft.AspNetCore.Mvc;

namespace CW_7_s31105.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TripController(IDbService dbService): ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllTrips()
    {
        var trips = await dbService.GetTripDetailsAsync();
        return Ok(trips);
    }
}