using System;
using System.Linq;

namespace GiftDrafter;

public abstract class Strictness
{
    public abstract string DisplayName { get; }
    public abstract bool CanDraft(Participant drafter, Assignment assignment, DraftRound draftRound);
}

public class MinimumStrictness : Strictness
{
    public override string DisplayName => "Minimum";

    public override bool CanDraft(Participant drafter, Assignment assignment, DraftRound draftRound)
    {
        if (drafter is null)
            return false;
        return assignment.Receiver != drafter.Person;
    }
}

public class LowStrictness : Strictness
{
    public override string DisplayName => "Low";

    public override bool CanDraft(Participant drafter, Assignment assignment, DraftRound draftRound)
    {
        if (drafter is null)
            return false;
        if (assignment.Receiver == drafter.Person) 
            return false;
        return drafter.Assignments.All(a => a.Receiver != assignment.Receiver);
    }
}

public class MediumStrictness : Strictness
{
    public override string DisplayName => "Medium";

    public override bool CanDraft(Participant drafter, Assignment assignment, DraftRound draftRound)
    {
        if (drafter is null)
            return false;
        if (assignment.Receiver == drafter.Person) 
            return false;
        return !drafter.Assignments.Any(a => a.Receiver == assignment.Receiver || a.Category == assignment.Category);
    }
}

public class HighStrictness : Strictness
{
    public override string DisplayName => "High";

    public override bool CanDraft(Participant drafter, Assignment assignment, DraftRound draftRound)
    {
        if (drafter is null || draftRound is null)
            return false;
        if (assignment.Receiver == drafter.Person) 
            return false;
        if (drafter.Assignments.Any(a => a.Receiver == assignment.Receiver || a.Category == assignment.Category))
            return false;

        if (draftRound.Participants.Count == 2)
        {
            var givers = draftRound.MadeAssignments.Select(p => p.Giver).ToHashSet();
            var receivers = draftRound.MadeAssignments.Select(ma => ma.Receiver).ToHashSet();
            givers.Add(drafter.Person);
            receivers.Add(assignment.Receiver);

            if (receivers.SetEquals(givers))
                return false;
        }

        return !draftRound.MadeAssignments.Select(par => par.Receiver).Contains(assignment.Receiver);
    }
}