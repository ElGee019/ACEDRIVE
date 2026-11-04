using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The community centre's post-intro hiring page (PORTPLAN 54, Unity
/// CommunityCentre's hire mode): HIRE AN EMPLOYEE reveals the week's residents,
/// a row hire banks the name through the roster, the confirmation replaces the
/// rows, and BACK leaves through the host's command.
/// </summary>
public sealed class CommunityCentreScreenViewModelTests
{
    private static readonly string[] Town =
    [
        "Anna Berg", "Bram de Wit", "Carla Rossi", "Daan Vos", "Elsa Meyer", "Finn Marsh",
    ];

    public CommunityCentreScreenViewModelTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    private static (CommunityCentreScreenViewModel Vm, FakeEmployeeStore Store) Create(
        IReadOnlyList<string>? residents = null,
        IFriendshipStore? friendships = null,
        Action<string>? hired = null,
        Action? back = null,
        int week = 1)
    {
        var store = new FakeEmployeeStore();
        var roster = new EmployeeRoster(store, new Random(7));
        IReadOnlyList<string> town = residents ?? Town;
        roster.RefreshCandidates(town, week);
        var vm = new CommunityCentreScreenViewModel(
            roster, () => town, () => week,
            friendships ?? new FakeFriendshipStore(),
            new RelayCommand(back ?? (() => { })),
            new VoiceBeat(new FakeClock()),
            hired ?? (_ => { }));
        return (vm, store);
    }

    [Fact]
    public void Page_StartsOnTheHireButtonWithoutRows()
    {
        var (vm, _) = Create();

        Assert.False(vm.HasCandidates);
        Assert.False(vm.ShowEmpty);
        Assert.False(vm.ShowHired);
        Assert.Contains(Strings.Get("CommunityCentreHire"), vm.VoiceActions.Keys);
        Assert.Contains(Strings.Get("CommunityCentreBack"), vm.VoiceActions.Keys);
    }

    [Fact]
    public void ShowCandidates_RevealsFiveResidentsWithTheirFamiliarity()
    {
        var friendships = new FakeFriendshipStore();
        for (int i = 0; i < Town.Length; i++)
        {
            friendships.AddFamiliarity(Town[i], i + 1);
        }

        var (vm, _) = Create(friendships: friendships);
        vm.ShowCandidatesCommand.Execute(null);

        Assert.True(vm.HasCandidates);
        Assert.False(vm.ShowEmpty);
        Assert.Equal(EmployeeRoster.CandidateCount, vm.Candidates.Count);
        foreach (HireCandidateRow row in vm.Candidates)
        {
            int level = Array.IndexOf(Town, row.Name) + 1;
            Assert.Equal(Strings.Format("CommunityCentreFriendship", level), row.FriendshipLine);
            // Every candidate is speakable by name while the rows are up.
            Assert.Contains(row.Name, vm.VoiceActions.Keys);
        }
    }

    [Fact]
    public void Hire_BanksTheCandidateWarmsTheHostAndShowsTheConfirmation()
    {
        string? hired = null;
        var (vm, store) = Create(hired: name => hired = name);
        vm.ShowCandidatesCommand.Execute(null);
        HireCandidateRow row = vm.Candidates[0];

        vm.HireCommand.Execute(row);

        Assert.Equal(row.Name, hired);
        Assert.True(vm.ShowHired);
        Assert.Equal(Strings.Format("CommunityCentreHired", row.Name), vm.HiredLine);
        Assert.False(vm.HasCandidates);
        Assert.False(vm.ShowEmpty);
        Assert.Contains(row.Name, store.State.Employees!);
        Assert.DoesNotContain(row.Name, vm.VoiceActions.Keys);
    }

    [Fact]
    public void EmptyTown_ShowsTheNoWorkLineInsteadOfRows()
    {
        var (vm, _) = Create(residents: []);

        vm.ShowCandidatesCommand.Execute(null);

        Assert.True(vm.ShowEmpty);
        Assert.False(vm.HasCandidates);
        Assert.Empty(vm.Candidates);
    }

    [Fact]
    public void Back_RunsTheHostsBackCommand()
    {
        bool backed = false;
        var (vm, _) = Create(back: () => backed = true);

        vm.BackCommand.Execute(null);

        Assert.True(backed);
        Assert.False(vm.BackFlash);
    }
}
