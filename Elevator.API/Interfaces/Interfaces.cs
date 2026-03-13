using Elevator.API.Models;

namespace Elevator.API.Interfaces
{
    public interface IElevatorDispatchService
    {
        AssignedElevatorEvent? Assign(
            TripRequestMessage request,
            IReadOnlyList<Elevators> elevators,
            IReadOnlyList<ElevatorRuntimeState> runtimeStates);
    }
}
