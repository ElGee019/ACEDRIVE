using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The assign-a-job page the employees page's NOT ON JOB button opens (PORTPLAN
/// item 23): the day's car-sharing requests above three work-family buttons —
/// CAR SHARING, TAXI JOB and DELIVERY JOB — and a BACK button at the bottom.
/// Picking a family assigns the employee through the family's own service —
/// <see cref="CarShareJobs"/> for the day's batch, <see cref="EmployeeRides"/>
/// for a house-to-house ride, <see cref="EmployeeDeliveries"/> for a store
/// refill — and returns to the employees page, where that employee's row now
/// reads ON JOB. BACK without a pick returns to the employees page unchanged.
/// A family whose button has a finished job waiting paints its text teal
/// through <see cref="EmployeeJobBoard"/>, exactly as it did on the old
/// employees page.
///
/// The car-sharing block is a reading of <see cref="CarShareRequestPlanner"/>'s
/// day: the page never draws a ride itself, so the same batch is shown for
/// every employee and a rebuilt page cannot reshuffle the town. Picking CAR
/// SHARING hands the batch to <see cref="CarShareJobs"/>: its last arrival is
/// the minute the row flips to ARRIVED, and the batch's XP total is what the
/// collect banks. A quiet day leaves the employee waiting on the family with
/// nothing to collect: the town's schedule is set every night, so the row stays
/// ON JOB until a real batch runs and cannot arrive for rides nobody drove.
/// </summary>
public sealed class AssignJobScreenViewModel
    : ViewModelBase, IVoiceScreen
{
    private readonly string _employeeName;
    private readonly EmployeeRides _rides;
    private readonly EmployeeDeliveries _deliveries;
    private readonly CarShareJobs _carShare;
    private readonly EmployeeJobBoard _jobs;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;
    private readonly IReadOnlyList<CarShareRequest> _batch;

    public AssignJobScreenViewModel(
        string employeeName,
        EmployeeRides rides,
        EmployeeDeliveries deliveries,
        CarShareJobs carShare,
        EmployeeJobBoard jobs,
        IReadOnlyList<CarShareRequest> carShareRequests,
        ICommand backCommand,
        VoiceBeat beat)
    {
        ArgumentNullException.ThrowIfNull(employeeName);
        ArgumentNullException.ThrowIfNull(rides);
        ArgumentNullException.ThrowIfNull(deliveries);
        ArgumentNullException.ThrowIfNull(carShare);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(carShareRequests);
        ArgumentNullException.ThrowIfNull(backCommand);
        ArgumentNullException.ThrowIfNull(beat);

        _employeeName = employeeName;
        _rides = rides;
        _deliveries = deliveries;
        _carShare = carShare;
        _jobs = jobs;
        _backCommand = backCommand;
        _beat = beat;
        _batch = carShareRequests;
        CarSharingCommand = new RelayCommand(() => Assign(EmployeeJobKind.CarSharing));
        TaxiCommand = new RelayCommand(() => Assign(EmployeeJobKind.TaxiJobs));
        DeliveryCommand = new RelayCommand(() => Assign(EmployeeJobKind.DeliveryJobs));
        BackCommand = new RelayCommand(Back);
        CarSharingFinished = _jobs.HasFinished(EmployeeJobKind.CarSharing);
        TaxiJobsFinished = _jobs.HasFinished(EmployeeJobKind.TaxiJobs);
        DeliveryJobsFinished = _jobs.HasFinished(EmployeeJobKind.DeliveryJobs);
        CarShareRequests = BuildRows(carShareRequests);
        RebuildVoice();
    }

    /// <summary>The employee the job is being assigned to.</summary>
    public string EmployeeName => _employeeName;

    /// <summary>Assigns the car-sharing family and returns to the employees page.</summary>
    public ICommand CarSharingCommand { get; }

    /// <summary>Assigns the taxi family and returns to the employees page.</summary>
    public ICommand TaxiCommand { get; }

    /// <summary>Assigns the delivery family and returns to the employees page.</summary>
    public ICommand DeliveryCommand { get; }

    /// <summary>Returns to the employees page without assigning a job.</summary>
    public ICommand BackCommand { get; }

    /// <summary>True while a car-sharing job has finished and waits to be seen.</summary>
    public bool CarSharingFinished { get; }

    /// <summary>True while a taxi job has finished and waits to be seen.</summary>
    public bool TaxiJobsFinished { get; }

    /// <summary>True while a delivery job has finished and waits to be seen.</summary>
    public bool DeliveryJobsFinished { get; }

    /// <summary>
    /// Today's car-sharing requests, already shaped for the page. The block
    /// above the CAR SHARING button lists them; the empty state is a copy line,
    /// so this is empty rather than null on a quiet day.
    /// </summary>
    public IReadOnlyList<CarShareRequestRow> CarShareRequests { get; }

    /// <summary>True when the town asked for at least one ride today.</summary>
    public bool HasCarShareRequests => CarShareRequests.Count > 0;

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash
    {
        get => _backFlash;
        private set => SetField(ref _backFlash, value);
    }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    // One path out for every pick and for BACK: the employee is assigned (or
    // not) and the page returns to the employees page, clearing the glow. Each
    // family owns its own timing: car sharing reads the day's batch, a taxi job
    // draws a house-to-house ride, and a delivery targets one store's refill.
    private void Assign(EmployeeJobKind kind)
    {
        switch (kind)
        {
            case EmployeeJobKind.CarSharing:
                _carShare.Assign(_employeeName, _batch);
                break;
            case EmployeeJobKind.TaxiJobs:
                _rides.Assign(_employeeName);
                break;
            case EmployeeJobKind.DeliveryJobs:
                _deliveries.Assign(_employeeName);
                break;
        }

        Back();
    }

    private void Back()
    {
        BackFlash = false;
        _backCommand.Execute(null);
    }

    // The page's own view of the day: units and separators are resx shapes, so
    // a translator can reorder a row without the arithmetic moving.
    private static List<CarShareRequestRow> BuildRows(
        IReadOnlyList<CarShareRequest> requests)
    {
        var rows = new List<CarShareRequestRow>(requests.Count);
        foreach (CarShareRequest request in requests)
        {
            rows.Add(new CarShareRequestRow(
                Strings.Format(
                    "AssignJobCarShareTime",
                    request.LeaveMinute / 60,
                    request.LeaveMinute % 60),
                request.CustomerName,
                Strings.Format(
                    "AssignJobCarShareRoute",
                    AddressFormatter.Format(request.Pickup),
                    AddressFormatter.Format(request.Dropoff)),
                Strings.Format("AssignJobCarShareDistance", request.DistanceKm)));
        }

        return rows;
    }

    private void RebuildVoice()
    {
        VoiceActions = new Dictionary<string, Action>
        {
            // The three work buttons act at once like a click: assign and
            // return to the employees page.
            [Strings.Get("AssignJobCarSharing")] = () => Assign(EmployeeJobKind.CarSharing),
            [Strings.Get("AssignJobTaxi")] = () => Assign(EmployeeJobKind.TaxiJobs),
            [Strings.Get("AssignJobDelivery")] = () => Assign(EmployeeJobKind.DeliveryJobs),
            // Back navigates: glow first, act at the end of the shared beat.
            [Strings.Get("AssignJobBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
    }
}
