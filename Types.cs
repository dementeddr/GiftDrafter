using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace GiftDrafter;

public class Participant(Person person) : ObservableObject
{
    public Person Person { get; } = person;

    public ObservableCollection<Assignment> Assignments { get; } = [];

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendFormat("Participant {0}\n", Person.Name);
        foreach (var assignment in Assignments)
            sb.AppendLine(assignment.Display);
        return sb.ToString();
    }
}


public class Person : ObservableObject
{
    public Person(string name)
    {
        Name = name.Trim();
        IsValid = true;
    }

    public string Name { get; }

    public bool IsValid { get; set => SetProperty(ref field, value); }

    public override string ToString() { return Name; }
}


public class Category : ObservableObject
{
    public Category(string desc)
    {
        Desc = desc.Trim();
        IsValid = true;
    }

    public string Desc { get; }

    public bool IsValid { get; set => SetProperty(ref field, value); }

    public override string ToString() { return Desc; }
}


public class Assignment : ObservableObject
{
    public Assignment(Person receiver, Category category)
    {
        Category = category;
        Receiver = receiver;
        Display = $"  {Receiver}  <-  {Category}";
    }

    public Person Giver { get; set => SetProperty(ref field, value); }
    public Person Receiver { get; }
    public Category Category { get; }

    public bool IsValid { get; set => SetProperty(ref field, value); }

    public string Display { get; }

    public override string ToString()
    {
        return Display;
    }
}


public class DraftRound(IEnumerable<Participant> participants, int roundNumber) : ObservableObject
{
    public ObservableCollection<Participant> Participants { get; } = new(participants);
    public ObservableCollection<Assignment> MadeAssignments { get; } = [];

    public int RoundNumber { get; } = roundNumber;
    public Participant CurrentParticipant { get; set => SetProperty(ref field, value); }

    public bool IsActive
    {
        get;
        set
        {
            SetProperty(ref field, value);
            CurrentParticipant = Participants.FirstOrDefault();
        }
    }

}