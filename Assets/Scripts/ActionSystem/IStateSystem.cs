namespace Sol
{
    /// <summary>
    /// Standard lifecycle contract for all continuous state systems.
    /// Actions call Enter/Exit to transition; the system ticks independently.
    /// </summary>
    public interface IStateSystem
    {
        bool IsActive { get; }
        void Enter();
        void Exit();
    }
}
