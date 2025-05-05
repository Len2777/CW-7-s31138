using CW7APBD.Services;
 

namespace CW7APBD.Controllers;
using Microsoft.AspNetCore.Mvc;
 
using System.Threading.Tasks;

[ApiController]
[Route("[controller]")]
public class TripController(IDbService service) : ControllerBase
{
   
    [HttpGet]
    public async Task<IActionResult> GetAllTrips() {
        return Ok(await service.GetTripsDetailsAsync());
    }
}