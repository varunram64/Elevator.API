using Elevator.API.Interfaces;
using Elevator.API.Models;
using Elevator.API.Repository;

namespace Elevator.API.Services;

public sealed class ElevatorDispatchService : IElevatorDispatchService
{
    public AssignedElevatorEvent? Assign(
        TripRequestMessage request,
        IReadOnlyList<Elevators> elevators,
        IReadOnlyList<ElevatorRuntimeState> runtimeStates)
    {
        var active = elevators
            .Where(e => e.ServiceStatus == ElevatorServiceStatus.Active)
            .ToList();

        if (!active.Any())
            return null;

        Elevators? bestElevator = null;
        ElevatorRuntimeState? bestState = null;
        int bestDistance = int.MaxValue;

        foreach (var elevator in active)
        {
            var state = runtimeStates.FirstOrDefault(s => s.ElevatorId == elevator.Id)
                        ?? new ElevatorRuntimeState
                        {
                            ElevatorId = elevator.Id,
                            CurrentFloor = 0,
                            CurrentDirection = null,
                            IsBusy = false,
                            LastUpdatedUtc = DateTime.UtcNow
                        };

            if (state.IsBusy && state.CurrentDirection.HasValue)
            {
                if (state.CurrentDirection != request.Direction)
                    continue;

                if (request.Direction == Direction.Up &&
                    state.CurrentFloor > request.SourceFloor)
                    continue;

                if (request.Direction == Direction.Down &&
                    state.CurrentFloor < request.SourceFloor)
                    continue;
            }

            var distance = Math.Abs(state.CurrentFloor - request.SourceFloor);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestElevator = elevator;
                bestState = state;
            }
        }

        if (bestElevator is null || bestState is null)
            return null;

        return new AssignedElevatorEvent(
            request.RequestId,
            bestElevator.Id,
            bestElevator.Name,
            bestState.CurrentFloor,
            request.Direction,
            request.SourceFloor,
            request.DestinationFloor,
            DateTime.UtcNow);
    }
}
