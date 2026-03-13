namespace Elevator.API.Models
{
    public sealed class ElevatorRuntimeState
    {
        public int ElevatorId { get; init; }
        public int CurrentFloor { get; set; }
        public Direction? CurrentDirection { get; set; }
        public bool IsBusy { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }

    public interface IElevatorStateCache
    {
        ElevatorRuntimeState? GetState(int elevatorId);
        IReadOnlyList<ElevatorRuntimeState> GetAll();
        void Upsert(ElevatorRuntimeState state);
    }

    public sealed class ElevatorStateCache : IElevatorStateCache
    {
        private readonly Dictionary<int, ElevatorRuntimeState> _states = new();

        public ElevatorRuntimeState? GetState(int elevatorId) =>
            _states.TryGetValue(elevatorId, out var s) ? s : null;

        public IReadOnlyList<ElevatorRuntimeState> GetAll() => _states.Values.ToList();

        public void Upsert(ElevatorRuntimeState state)
        {
            _states[state.ElevatorId] = state;
        }
    }

}
