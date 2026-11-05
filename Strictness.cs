using System.Linq;

namespace GiftDrafter;

public enum Strictness
{
    Narcissist,
    Low,
    Medium,
    High,
}

public static class StrictnessExtensions
{
    public static bool CanBeAssigned(this Participant participant, Assignment assignment)
    {
        if (participant is null)
            return false;
        if (assignment.Receiver == participant.Person) 
            return false;
        if (participant.Assignments.Any(a => a.Receiver == assignment.Receiver || a.Category == assignment.Category))
            return false;

        return true;
    }
}