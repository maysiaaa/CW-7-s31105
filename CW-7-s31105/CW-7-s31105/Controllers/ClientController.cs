using CW_7_s31105.Exceptions;
using CW_7_s31105.Models.DTOs;
using CW_7_s31105.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CW_7_s31105.Controllers;


[ApiController]
[Route("api/[controller]")]
public class ClientController(IDbService dbService): ControllerBase
{
    
    [HttpGet("{id}/trips")]
    public async Task<IActionResult> GetClientTrips(
        [FromRoute] int id
    )
    {
        try
        {
            var trips = await dbService.GetClientTripsAsync(id);
            return Ok(trips);
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateClient(
        [FromBody] ClientCreateDTO body
    )
    {
        try
        {
            var clientId = await dbService.CreateClientAsync(body);
            return Created($"api/clients/{clientId}", new { IdClient = clientId });
        }
        catch (Exception e)
        {
            return BadRequest(e.Message); 
        }
    }
    
    [HttpPut("{id}/trips/{tripId}")]
    public async Task<IActionResult> RegisterClientForTrip(int id, int tripId)
    {
        try
        {
            var result = await dbService.RegisterClientForTripAsync(id, tripId);
            return Ok("Client successfully registered for the trip.");
        }
        catch (SqlException e) when (e.Number == 50000)
        {
            return BadRequest(e.Message);
        }
        catch (Exception e)
        {
            return StatusCode(500, "An unexpected error occurred.");
        }
    }
    
    [HttpDelete("{id}/trips/{tripId}")]
    public async Task<IActionResult> UnregisterClientFromTrip(int id, int tripId)
    {
        try
        {
            var result = await dbService.UnregisterClientFromTripAsync(id, tripId);
            return Ok("Client successfully unregistered from the trip.");
        }
        catch (SqlException e) when (e.Number == 50000)
        {
            return NotFound(e.Message); // lub BadRequest, zależnie od interpretacji
        }
        catch (Exception)
        {
            return StatusCode(500, "An unexpected error occurred.");
        }
    }
    
}