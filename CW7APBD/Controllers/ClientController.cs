using CW7APBD.Exeptions;
using CW7APBD.Models.Dtos;
using CW7APBD.Services;

namespace CW7APBD.Controllers;

using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

 

[ApiController]
[Route("[controller]")]
public class ClientsController(IDbService dbService) : ControllerBase
{
    // Zwraca listę wycieczek, na które zapisany jest dany klient
    [HttpGet("{id}/trips")]
    public async Task<IActionResult> GetClientTrips([FromRoute] int id) {
        try {
            return Ok(await dbService.GetClientTripsAsync(id));
        } catch(NotFoundException e) {
            return NotFound(e.Message);
        }
    }
    // Tworzy nowego klienta i zwraca jego dane
    [HttpPost]
    public async Task<IActionResult> AddClient([FromBody] ClientAdd clientDTO) {
        try {
            var client = await dbService.AddClientAsync(clientDTO);
            return Created($"clients/{client.IdClient}", client);
        } catch (NotFoundException e) {
            return NotFound(e.Message);
        }
    }
    // Rejestruje klienta na wybraną wycieczkę
    [HttpPut("{clientID}/trips/{tripID}")]
    public async Task<IActionResult> RegisterClientToTrip([FromRoute] int clientID, [FromRoute]int tripID) {
        try {
            await dbService.RegisterClientForTripAsync(clientID, tripID);
            return NoContent();
        } catch (NotFoundException e) {
            return NotFound(e.Message);
        }
    }
    // Usuwa klienta z wycieczki (anuluje rejestrację)

    [HttpDelete("{clientID}/trips/{tripID}")]
    public async Task<IActionResult> RemoveRegistration([FromRoute] int clientID, [FromRoute] int tripID) {
        try {
            await dbService.DeregisterClientFromTripAsync(clientID, tripID);
            return NoContent();
        } catch (NotFoundException e) {
            return NotFound(e.Message);
        }
    }
}