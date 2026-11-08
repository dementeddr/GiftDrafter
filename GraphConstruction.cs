using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable LocalizableElement

namespace GiftDrafter;

public static class GraphConstruction
{
    public static void ShuffleList<T>(this Random rng, List<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public static bool ConstructRemainingGraph(this MainWindowViewModel vm)
    {
        var participants = vm.Participants.ToList();
        var pending = vm.PendingAssignments.ToList();
        var slots = new List<AssignmentSlot>(pending.Count);
        var categoryCount = vm.Categories.Count;

        vm.Rng.ShuffleList(participants);
        vm.Rng.ShuffleList(pending);

        foreach (var assignment in pending) assignment.IsValid = true;

        slots.AddRange(
            from participant in participants 
            from i in Enumerable.Range(participant.Assignments.Count, categoryCount - participant.Assignments.Count) 
            select new AssignmentSlot(participant, i)
            );

        // Console.WriteLine("Participants:");
        // foreach (var participant in participants) Console.WriteLine(participant);
        //
        // Console.WriteLine("\nSlots:");
        // foreach (var assignmentSlot in slots) Console.WriteLine("{0}: {1}", assignmentSlot.Participant.Person.Name, assignmentSlot.Slot);
        //
        // Console.WriteLine("\nPending Assignments:");
        // foreach (var assignment in pending) Console.WriteLine(assignment.Display);
        //
        // Console.WriteLine("\nBEGIN:\n");

        return AssignTo(0);
        

        bool AssignTo(int depth)
        {
            if (depth >= pending.Count)
                return true;
                
            var participant = slots[depth].Participant;

            foreach (var (assignment, i) in pending.Where(p => p.IsValid).Select((p, i) => (p, i)))
            {
                var canAssign = vm.SelectedStrictness.CanDraft(participant, assignment, vm.DraftOrder.FirstOrDefault());
                // Console.WriteLine("{0}-{1} {2}: {3} {4} -> {5}: {6}", depth, i, canAssign, participant.Person.Name, slots[depth].Slot, assignment.Receiver.Name, assignment.Category.Desc);

                if (canAssign)
                {
                    assignment.IsValid = false;
                    participant.Assignments.Add(assignment);

                    if (AssignTo(depth + 1))
                        return true;

                    participant.Assignments.RemoveAt(slots[depth].Slot);
                }

                assignment.IsValid = true;
            }

            return false;
        }
    }
}

internal class AssignmentSlot(Participant participant, int slot)
{
    public Participant Participant { get; } = participant;

    public int Slot { get; } = slot;
}