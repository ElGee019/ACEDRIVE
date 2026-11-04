// The employees' car-sharing work (PORTPLAN items 22 + 23). The town sets its
// shared-car schedule every night, so a quiet day is not a finished job: the
// employee stays on the car-sharing family until real rides run, and that later
// batch is what finally pays them.
namespace AceDrive.Core;

/// <summary>
/// Puts employees on the town's car-sharing batch and arms the ones still
/// waiting for one. The rule itself is <see cref="CarShareRules.AssignmentFor"/>
/// — a batch is workable while its last ride is still ahead — and this class
/// only applies it to the crew, writing through <see cref="EmployeeAssignments"/>,
/// which stays the one owner of the on-job marks.
/// </summary>
public sealed class CarShareJobs
{
    private readonly EmployeeAssignments _assignments;
    private readonly GameClock _game;

    public CarShareJobs(EmployeeAssignments assignments, GameClock game)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(game);

        _assignments = assignments;
        _game = game;
    }

    /// <summary>
    /// Puts the employee on the day's batch, or on the family with no finish
    /// when the town booked nothing (or today's rides already left): the row
    /// then reads ON JOB and nothing can be collected until a real batch runs.
    /// </summary>
    public void Assign(string? employeeName, IReadOnlyList<CarShareRequest> batch) =>
        _assignments.Assign(employeeName, ShiftFor(batch));

    /// <summary>
    /// Gives every employee waiting on the shared car today's real batch, so a
    /// quiet-day assignment still finishes once the town books rides again.
    /// Called when the employees page refreshes — a cold path, never the poll.
    /// </summary>
    public void ArmWaiting(IReadOnlyList<string> crew, IReadOnlyList<CarShareRequest> batch)
    {
        ArgumentNullException.ThrowIfNull(crew);

        EmployeeJobAssignment shift = ShiftFor(batch);
        if (shift.DueGameMinute is null)
        {
            // Nothing workable today: every waiting row keeps waiting instead
            // of arriving for rides the town never booked.
            return;
        }

        foreach (string name in crew)
        {
            if (_assignments.AssignmentOf(name)
                is { Kind: EmployeeJobKind.CarSharing, DueGameMinute: null })
            {
                _assignments.Assign(name, shift);
            }
        }
    }

    private EmployeeJobAssignment ShiftFor(IReadOnlyList<CarShareRequest> batch) =>
        CarShareRules.AssignmentFor(batch, _game.GameMinute, _game.GameMinuteOfDay);
}
