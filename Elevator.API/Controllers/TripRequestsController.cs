using Elevator.API.Models;
using Elevator.API.Producers;
using Elevator.API.Repository;
using Elevator.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Elevator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TripRequestsController : ControllerBase
{
    private readonly ITripRequestProducer _producer;

    public TripRequestsController(ITripRequestProducer producer)
    {
        _producer = producer;
    }

    [HttpPost]
    public IActionResult Create([FromBody] TripRequestDto request)
    {
        var message = new TripRequestMessage(
            RequestId: Guid.NewGuid(),
            Direction: request.Direction,
            CurrentFloor: request.CurrentFloor,
            SourceFloor: request.SourceFloor,
            DestinationFloor: request.DestinationFloor,
            RequestedAtUtc: DateTime.UtcNow
        );

        _producer.Publish(message);

        return Accepted(new { RequestId = message.RequestId });
    }
}


