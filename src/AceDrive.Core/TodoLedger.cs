// The ToDo engine (PORTPLAN item 46, port of Unity Todo/TodoManager.cs): four
// visible tips, a rotation queue, per-subject targets and the completion latch
// that feeds the care-package grant. Pure state: the store and the save are
// the caller's; time, randomness and copy never enter here.
//
// WHY the maps are keyed by subject id instead of Unity's fixed index arrays:
// a fixed array makes every added subject a migration (Unity patches five
// different lists on load). Keyed maps make a new subject start at its own
// defaults and an unknown id drop out, which is the same repair with less
// code and no schema version.
namespace AceDrive.Core;

public sealed class TodoLedger
{
    /// <summary>How many tips the jobs page shows at once (Unity MaxVisibleSlots).</summary>
    public const int MaxVisibleSlots = 4;

    /// <summary>Settled jobs one tip may sit unfulfilled before it rotates out.</summary>
    public const int TurnLimit = 6;

    /// <summary>
    /// Completed tips the list holds at once (Unity TodoManager.MaxCarePackages,
    /// the cap on outstanding care packages). A completion met while the list is
    /// full is lost.
    /// </summary>
    public const int MaxCompletedTips = 7;

    // Unity's guard loops: a stacked completion may need many cycles before
    // the player stops meeting the target, and a bad target curve must not
    // hang the UI thread.
    private const int CycleSafety = 64;
    private const int AcknowledgeSafety = 32;

    private readonly List<TodoSlotState> _visible = [];
    private readonly List<string> _queue = [];
    private readonly Dictionary<string, double> _targets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _progress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _bankInterest = new(StringComparer.Ordinal);
    private readonly HashSet<string> _awaiting = new(StringComparer.Ordinal);
    private readonly HashSet<string> _shown = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activated = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TodoTipSnapshot> _completedTips = new(StringComparer.Ordinal);

    // Subjects acknowledged in the current Acknowledge pass: the Sync that
    // follows re-flags a one-time subject whose flag persists, and that
    // re-flag must not read as a fresh completion (Unity _justAcknowledged).
    private readonly HashSet<string> _acknowledged = new(StringComparer.Ordinal);

    private TodoLedger()
    { }

    /// <summary>Changes on every mutation, so a cached page can guard its repaint.</summary>
    public int Version { get; private set; }

    /// <summary>True while a completed subject still waits to be delivered.</summary>
    public bool HasCompleted => _awaiting.Count > 0;

    /// <summary>How many completed tips the popup lists right now.</summary>
    public int CompletedCount => _awaiting.Count;

    /// <summary>
    /// A fresh career: the table's entries are the first four slots, every
    /// other non-one-time subject waits in the queue (Unity Reset).
    /// </summary>
    public static TodoLedger Fresh(TodoTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var ledger = new TodoLedger();
        foreach (string subject in TodoSubject.All)
        {
            ledger.AddDefaults(subject);
        }

        foreach (TodoEntry entry in table.Entries)
        {
            if (ledger._visible.Count >= MaxVisibleSlots)
            {
                break;
            }

            if (!TodoRequirements.IsRegistered(entry.SubjectId)
                || ledger.IsVisible(entry.SubjectId))
            {
                continue;
            }

            ledger._visible.Add(new TodoSlotState(entry.SubjectId, 0));
            if (entry.Target > 0.0)
            {
                ledger._targets[entry.SubjectId] = entry.Target;
            }
        }

        ledger.FillQueue();
        ledger.FillEmptySlots();
        return ledger;
    }

    /// <summary>
    /// The saved career. A save with no usable slot is a fresh career; every
    /// unknown id is dropped and every subject the save predates joins the
    /// queue at its own defaults (the keyed-map migration).
    /// </summary>
    public static TodoLedger Resume(TodoTable table, TodoLedgerState? saved)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (saved is null)
        {
            return Fresh(table);
        }

        var ledger = new TodoLedger();
        foreach (string subject in TodoSubject.All)
        {
            ledger.AddDefaults(subject);
        }

        foreach (TodoSlotState slot in saved.Visible)
        {
            if (ledger._visible.Count >= MaxVisibleSlots)
            {
                break;
            }

            if (!TodoRequirements.IsRegistered(slot.SubjectId)
                || ledger.IsVisible(slot.SubjectId))
            {
                continue;
            }

            ledger._visible.Add(new TodoSlotState(slot.SubjectId, Math.Max(0, slot.Turns)));
        }

        if (ledger._visible.Count == 0)
        {
            return Fresh(table);
        }

        RestoreMap(saved.Targets, ledger._targets, value => value > 0.0);
        RestoreMap(saved.Progress, ledger._progress, value => value >= 0);
        RestoreMap(saved.BankInterests, ledger._bankInterest, value => value >= 0);
        RestoreSet(saved.Awaiting, ledger._awaiting);
        RestoreSet(saved.Shown, ledger._shown);
        RestoreSet(saved.Activated, ledger._activated);
        foreach ((string subject, TodoTipSnapshot tip) in saved.CompletedTips)
        {
            if (TodoRequirements.IsRegistered(subject) && tip is not null)
            {
                ledger._completedTips[subject] = tip;
            }
        }

        foreach (string subject in saved.Queue)
        {
            if (TodoRequirements.IsRegistered(subject)
                && !ledger.IsVisible(subject)
                && !ledger._queue.Contains(subject))
            {
                ledger._queue.Add(subject);
            }
        }

        ledger.FillQueue();
        ledger.FillEmptySlots();
        return ledger;
    }

    /// <summary>
    /// One sync against the career snapshot (Unity SyncWithPlayer): flag every
    /// newly met subject, then rotate a met tip out of its slot. Returns the
    /// subjects that completed for the first time — the care-package grant
    /// rides exactly those.
    /// </summary>
    public TodoSyncResult Sync(TodoFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        bool changed = false;
        List<string> completed = [];

        foreach (string subject in TodoSubject.All)
        {
            if (!_activated.Contains(subject))
            {
                continue;
            }

            ITodoRequirement requirement = TodoRequirements.Get(subject);
            if (requirement.IsOneTime && _shown.Contains(subject))
            {
                continue;
            }

            if (!requirement.IsMet(facts, _targets[subject]) || _awaiting.Contains(subject))
            {
                continue;
            }

            // The list holds MaxCompletedTips at once: a completion met while it
            // is full is lost (Unity MaxCarePackages). A hidden subject still
            // cycles past its met target here so it cannot resurface once space
            // frees; a visible one is cycled by the slot pass below.
            if (_awaiting.Count >= MaxCompletedTips)
            {
                if (!requirement.IsOneTime && !IsVisible(subject))
                {
                    CyclePastMet(subject, facts);
                    changed = true;
                }

                continue;
            }

            // One-directional latch: a later sync never drops a completion
            // before the popup has surfaced it (Unity's comment on the toggle).
            _awaiting.Add(subject);
            changed = true;
            if (_acknowledged.Contains(subject))
            {
                continue;
            }

            completed.Add(subject);
            // The target cycles right below, so capture the tip the player
            // actually met instead of letting the popup name the next one.
            _completedTips[subject] = new TodoTipSnapshot(
                requirement.TipKey, _targets[subject], requirement.Current(facts));
        }

        // A follow-up tip joins the list the moment its gate completes, at the
        // head of the queue so it takes the slot the gate just freed.
        changed |= UnlockGatedSubjects();

        for (int i = 0; i < _visible.Count; i++)
        {
            string subject = _visible[i].SubjectId;
            if (TodoRequirements.Get(subject).IsMet(facts, _targets[subject]))
            {
                changed |= ReplaceMetSlot(i, facts);
            }
        }

        if (changed)
        {
            Version++;
        }

        _acknowledged.Clear();
        return new TodoSyncResult(changed, completed, Version);
    }

    /// <summary>
    /// Deliver (Unity AcknowledgeAndCycle): clear every pending flag, cycle the
    /// threshold past any target the career still meets, retire one-time
    /// subjects and refill the slots. Returns whether anything moved.
    /// </summary>
    public bool Acknowledge(TodoFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        bool changed = false;

        // Hidden completions: flagged while the subject was in no visible slot
        // and not in the queue (TaxiJobs never rotates in). Cycle and return
        // progressive subjects to rotation.
        foreach (string subject in TodoSubject.All)
        {
            if (!_awaiting.Contains(subject)
                || IsVisible(subject)
                || _queue.Contains(subject))
            {
                continue;
            }

            _awaiting.Remove(subject);
            changed = true;
            _acknowledged.Add(subject);
            if (TodoRequirements.Get(subject).IsOneTime)
            {
                // One-time subjects are retired once the popup has surfaced
                // them, never re-flagged by a later sync (Unity shownInPopup).
                _shown.Add(subject);
                continue;
            }

            CyclePastMet(subject, facts);
            if (!_queue.Contains(subject))
            {
                _queue.Add(subject);
            }
        }

        // Subjects fulfilled while queued: one-time subjects retire, the rest
        // advance past the target. Iterate backwards so removals stay stable.
        for (int q = _queue.Count - 1; q >= 0; q--)
        {
            string subject = _queue[q];
            if (!_awaiting.Contains(subject))
            {
                continue;
            }

            _awaiting.Remove(subject);
            changed = true;
            _acknowledged.Add(subject);
            if (!TodoRequirements.Get(subject).IsMet(facts, _targets[subject]))
            {
                continue;
            }

            if (TodoRequirements.Get(subject).IsOneTime)
            {
                _shown.Add(subject);
                _queue.RemoveAt(q);
                continue;
            }

            CyclePastMet(subject, facts);
        }

        // Visible completions: the slot refills with the next un-met subject.
        int outer = AcknowledgeSafety;
        while (outer-- > 0)
        {
            bool cycled = false;
            for (int i = 0; i < _visible.Count; i++)
            {
                string subject = _visible[i].SubjectId;
                if (!_awaiting.Contains(subject))
                {
                    continue;
                }

                _awaiting.Remove(subject);
                changed = true;
                cycled = true;
                _acknowledged.Add(subject);
                bool oneTime = TodoRequirements.Get(subject).IsOneTime;
                if (oneTime)
                {
                    _shown.Add(subject);
                }

                CyclePastMet(subject, facts);
                if (TodoRequirements.Get(subject).IsMet(facts, _targets[subject]))
                {
                    // The safety limit ran out while the target is still met:
                    // keep it pending so the next popup surfaces it.
                    _awaiting.Add(subject);
                    _acknowledged.Add(subject);
                }

                if (!oneTime && !_queue.Contains(subject))
                {
                    _queue.Add(subject);
                }

                if (TakeNextCandidate(i, facts) is { } next)
                {
                    _visible[i] = new TodoSlotState(next, 0);
                }
            }

            if (!cycled)
            {
                break;
            }
        }

        if (changed)
        {
            Version++;
        }

        return changed;
    }

    /// <summary>
    /// One settled taxi or delivery job (Unity IncrementTurnCounter): every
    /// unfulfilled tip counts down; after six jobs it rotates out for the next
    /// queued subject. The Max grocery tip never rotates.
    /// </summary>
    public bool RotateOnJob(TodoFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        bool changed = false;
        for (int i = 0; i < _visible.Count; i++)
        {
            string subject = _visible[i].SubjectId;
            if (TodoRequirements.Get(subject).IsMet(facts, _targets[subject])
                || subject == TodoSubject.GoToMaxGroceryStore
                || !_activated.Contains(subject))
            {
                continue;
            }

            int turns = _visible[i].Turns + 1;
            changed = true;
            if (turns < TurnLimit)
            {
                _visible[i] = _visible[i] with { Turns = turns };
                continue;
            }

            string? replacement = TakeTurnCandidate(i);
            if (replacement is null)
            {
                _visible[i] = _visible[i] with { Turns = 0 };
                continue;
            }

            if (!_queue.Contains(subject))
            {
                _queue.Add(subject);
            }

            _visible[i] = new TodoSlotState(replacement, 0);
        }

        if (changed)
        {
            Version++;
        }

        return changed;
    }

    /// <summary>
    /// A door of the subject's type was placed (Unity ActivateSubject): the
    /// subject joins rotation. False when it was already active or unknown.
    /// </summary>
    public bool Activate(string subject)
    {
        if (!TodoRequirements.IsRegistered(subject) || !_activated.Add(subject))
        {
            return false;
        }

        if (!IsVisible(subject) && !_queue.Contains(subject))
        {
            _queue.Add(subject);
        }

        Version++;
        return true;
    }

    public bool IsActive(string subject) => _activated.Contains(subject);

    public bool IsPending(string subject) => _awaiting.Contains(subject);

    /// <summary>The four visible tips, ready to format.</summary>
    public IReadOnlyList<TodoSlotView> Slots(TodoFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var views = new List<TodoSlotView>(_visible.Count);
        foreach (TodoSlotState slot in _visible)
        {
            views.Add(View(slot.SubjectId, facts, captured: false));
        }

        return views;
    }

    /// <summary>The completed tips the popup lists, in subject order.</summary>
    public IReadOnlyList<TodoSlotView> CompletedTips(TodoFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var views = new List<TodoSlotView>(_awaiting.Count);
        foreach (string subject in InSubjectOrder(_awaiting))
        {
            views.Add(View(subject, facts, captured: true));
        }

        return views;
    }

    public TodoLedgerState Snapshot() => new(
        [.. _visible],
        [.. _queue],
        new Dictionary<string, double>(_targets, StringComparer.Ordinal),
        new Dictionary<string, int>(_progress, StringComparer.Ordinal),
        new Dictionary<string, int>(_bankInterest, StringComparer.Ordinal),
        InSubjectOrder(_awaiting),
        InSubjectOrder(_shown),
        InSubjectOrder(_activated),
        new Dictionary<string, TodoTipSnapshot>(_completedTips, StringComparer.Ordinal));

    // ---- internals ---------------------------------------------------------

    private void AddDefaults(string subject)
    {
        ITodoRequirement requirement = TodoRequirements.Get(subject);
        _targets[subject] = requirement.InitialTarget;
        _progress[subject] = 0;
        // Unity seeds the bank subject's interest allowance at 2, so the first
        // target is 1002 rather than the 1000 saldo floor.
        _bankInterest[subject] = subject == TodoSubject.BankStandings ? 2 : 0;
        if (TodoRequirements.IsInitiallyActive(subject))
        {
            _activated.Add(subject);
        }
    }

    // Unity Reset queues every non-one-time subject outside the four slots.
    // One-time subjects join only through Activate (their door was placed).
    private void FillQueue()
    {
        foreach (string subject in TodoSubject.All)
        {
            if (IsVisible(subject)
                || TodoRequirements.Get(subject).IsOneTime
                || _queue.Contains(subject))
            {
                continue;
            }

            _queue.Add(subject);
        }
    }

    private void FillEmptySlots()
    {
        // A resume can leave fewer than four usable slots (a retired one-time
        // subject, a save written by an older list). Inactive candidates go
        // back to the queue instead of being dropped: their door may be
        // placed later.
        int safety = _queue.Count + MaxVisibleSlots;
        while (_visible.Count < MaxVisibleSlots && _queue.Count > 0 && safety-- > 0)
        {
            string candidate = _queue[0];
            _queue.RemoveAt(0);
            if (!_activated.Contains(candidate) || IsVisible(candidate))
            {
                _queue.Add(candidate);
                continue;
            }

            _visible.Add(new TodoSlotState(candidate, 0));
        }
    }

    private bool IsVisible(string subject)
    {
        foreach (TodoSlotState slot in _visible)
        {
            if (string.Equals(slot.SubjectId, subject, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsVisibleElsewhere(string subject, int slotIndex)
    {
        for (int i = 0; i < _visible.Count; i++)
        {
            if (i != slotIndex
                && string.Equals(_visible[i].SubjectId, subject, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void CycleThreshold(string subject)
    {
        _progress[subject] += 1;
        ITodoRequirement requirement = TodoRequirements.Get(subject);
        TodoCycle next = requirement.Advance(
            new TodoCycle(_targets[subject], _progress[subject], _bankInterest[subject]));
        _targets[subject] = next.Target;
        _bankInterest[subject] = next.BankInterest;
    }

    // Advance until the career no longer meets the target: a stacked
    // completion (e.g. five jobs banked at once) must never pay twice.
    private void CyclePastMet(string subject, TodoFacts facts)
    {
        ITodoRequirement requirement = TodoRequirements.Get(subject);
        CycleThreshold(subject);
        int safety = CycleSafety;
        while (requirement.IsMet(facts, _targets[subject]) && safety-- > 0)
        {
            CycleThreshold(subject);
        }
    }

    // A gated tip (Unity's follow-up step): the moment its gate subject reaches
    // the completed block — pending or already delivered — the tip activates
    // and goes to the head of the queue, so the slot the gate frees this sync
    // refills with it instead of the next queued subject.
    private bool UnlockGatedSubjects()
    {
        bool changed = false;
        foreach (string subject in TodoSubject.All)
        {
            if (TodoRequirements.Get(subject) is not IUnlockedTodo gate
                || _activated.Contains(subject)
                || (!_awaiting.Contains(gate.UnlockedBy) && !_shown.Contains(gate.UnlockedBy)))
            {
                continue;
            }

            _activated.Add(subject);
            if (!IsVisible(subject) && !_queue.Contains(subject))
            {
                _queue.Insert(0, subject);
            }

            changed = true;
        }

        return changed;
    }

    // A met slot rotates out: the target advances, a progressive subject
    // rejoins the queue and the next un-met candidate takes the slot.
    private bool ReplaceMetSlot(int slotIndex, TodoFacts facts)
    {
        string subject = _visible[slotIndex].SubjectId;
        bool oneTime = TodoRequirements.Get(subject).IsOneTime;
        bool changed = false;
        CyclePastMet(subject, facts);
        if (!oneTime)
        {
            changed = true;
            if (!_queue.Contains(subject))
            {
                _queue.Add(subject);
            }
        }

        if (TakeNextCandidate(slotIndex, facts) is { } next)
        {
            _visible[slotIndex] = new TodoSlotState(next, 0);
            changed = true;
        }

        return changed;
    }

    // The next slot candidate: queued, active, not visible in another slot,
    // not the slot's current subject and not already met. Unity skips every
    // one of those for the same reason: a met subject belongs to the popup.
    private string? TakeNextCandidate(int slotIndex, TodoFacts facts)
    {
        int safety = _queue.Count + 1;
        while (_queue.Count > 0 && safety-- > 0)
        {
            string candidate = _queue[0];
            _queue.RemoveAt(0);
            if (string.Equals(candidate, _visible[slotIndex].SubjectId, StringComparison.Ordinal)
                || !_activated.Contains(candidate)
                || IsVisibleElsewhere(candidate, slotIndex)
                || TodoRequirements.Get(candidate).IsMet(facts, _targets[candidate]))
            {
                _queue.Add(candidate);
                continue;
            }

            return candidate;
        }

        return null;
    }

    // The turn rotation's candidate: like the met-slot pick, but a met subject
    // is still a legal tip (Unity's counter never checks the requirement).
    private string? TakeTurnCandidate(int slotIndex)
    {
        int safety = _queue.Count + 1;
        while (_queue.Count > 0 && safety-- > 0)
        {
            string candidate = _queue[0];
            _queue.RemoveAt(0);
            if (string.Equals(candidate, _visible[slotIndex].SubjectId, StringComparison.Ordinal)
                || !_activated.Contains(candidate)
                || IsVisibleElsewhere(candidate, slotIndex))
            {
                _queue.Add(candidate);
                continue;
            }

            return candidate;
        }

        return null;
    }

    private TodoSlotView View(string subject, TodoFacts facts, bool captured)
    {
        ITodoRequirement requirement = TodoRequirements.Get(subject);
        if (captured && _completedTips.TryGetValue(subject, out TodoTipSnapshot? tip))
        {
            return new TodoSlotView(subject, tip.CopyKey, tip.Target, tip.Current, requirement.PoiType);
        }

        return new TodoSlotView(
            subject, requirement.TipKey, _targets[subject], requirement.Current(facts), requirement.PoiType);
    }

    private static List<string> InSubjectOrder(IEnumerable<string> subjects)
    {
        var set = new HashSet<string>(subjects, StringComparer.Ordinal);
        var ordered = new List<string>(set.Count);
        foreach (string subject in TodoSubject.All)
        {
            if (set.Remove(subject))
            {
                ordered.Add(subject);
            }
        }

        // A retired or unknown id still rides the snapshot (never silently
        // dropped from a save the player owns).
        ordered.AddRange(set);
        return ordered;
    }

    private static void RestoreMap<T>(
        IReadOnlyDictionary<string, T> saved, Dictionary<string, T> target, Func<T, bool> valid)
    {
        foreach ((string subject, T value) in saved)
        {
            if (TodoRequirements.IsRegistered(subject) && valid(value))
            {
                target[subject] = value;
            }
        }
    }

    private static void RestoreSet(IReadOnlyList<string> saved, HashSet<string> target)
    {
        foreach (string subject in saved)
        {
            if (TodoRequirements.IsRegistered(subject))
            {
                target.Add(subject);
            }
        }
    }
}
