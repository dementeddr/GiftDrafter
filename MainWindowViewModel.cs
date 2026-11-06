using System;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows;
using System.Windows.Input;

namespace GiftDrafter;

public class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel()
    {
        OpenInputFileCommand = new RelayCommand(LoadInputFile);
        DraftPairCommand = new RelayCommand(DraftPair);
        ChooseAtRandomCommand = new RelayCommand(ChooseAtRandom);
        GenerateSeedCommand = new RelayCommand(GenerateSeed);
        SetSeedCommand = new RelayCommand(SetSeed);
        SaveAssignmentsCommand = new RelayCommand(SaveAssignments);
        GenerateRemainingCommand = new RelayCommand(GenerateRemainingGraph);

        SetSeed();
    }


    public ObservableCollection<Participant> Participants { get; } = [];
    public ObservableCollection<Person> People { get; } = [];
    public ObservableCollection<Category> Categories { get; } = [];
    public ObservableCollection<DraftRound> DraftOrder { get; } = [];
    public ObservableCollection<Assignment> PendingAssignments { get; } = [];
    public ObservableCollection<Assignment> MadeAssignments { get; } = [];
    public ObservableCollection<int> DraftRoundOptions { get; } = [1];

    public Participant DraftingParticipant
    {
        get => DraftOrder.FirstOrDefault()?.CurrentParticipant;
        set
        {
            var drafting = DraftOrder.FirstOrDefault();
            if (drafting is null || drafting.CurrentParticipant == value)
                return;

            drafting.CurrentParticipant = value;
            UpdateAssignmentValidity();
        }
    }

    public Assignment SelectedAssignment { get; set => SetProperty(ref field, value); }

    public Random Rng { get; set; }
    public string Seed { get; set => SetProperty(ref field, value); } = "0";
    public bool DraftNotStarted { get; private set => SetProperty(ref field, value); } = true;
    public int DraftRounds
    {
        get;
        set
        {
            SetProperty(ref field, value);
            GenerateDraftOrder();
        }
    } = 1;

    public ICommand OpenInputFileCommand { get; }
    public ICommand DraftPairCommand { get; }
    public ICommand ChooseAtRandomCommand { get; }
    public ICommand GenerateSeedCommand { get; }
    public ICommand SetSeedCommand { get; }
    public ICommand GenerateRemainingCommand { get; }
    public ICommand SaveAssignmentsCommand { get; }

    private void GenerateSeed()
    {
        Seed = new Random().Next().ToString();
        SetSeed();
    }

    private void SetSeed()
    {
        if (!int.TryParse(Seed, out var seedInt))
            seedInt = Seed.GetHashCode();

        Rng = new Random(seedInt);
        GenerateDraftOrder();
    }

    private void DraftPair()
    {
        var draftRound = DraftOrder.FirstOrDefault();

        if (DraftingParticipant is null || !(SelectedAssignment?.IsValid ?? false) || draftRound is null)
            return;

        DraftNotStarted = false;

        var assignment = SelectedAssignment;
        PendingAssignments.Remove(assignment);
        MadeAssignments.Add(assignment);
        DraftingParticipant.Assignments.Add(assignment);

        draftRound.Participants.Remove(DraftingParticipant);
        if (!draftRound.Participants.Any())
        {
            draftRound.PropertyChanged -= OnCurrentDrafterChanged;
            DraftOrder.RemoveAt(0);
            draftRound = DraftOrder.FirstOrDefault();
            draftRound?.IsActive = true;
            draftRound?.PropertyChanged += OnCurrentDrafterChanged;
        }

        DraftingParticipant = draftRound?.Participants.FirstOrDefault();
    }

    private void ChooseAtRandom()
    {
        var validChoices = PendingAssignments.Where(a => a.IsValid).ToArray();
        SelectedAssignment = validChoices[Rng.Next(validChoices.Length)];
    }

    #region FileAccess

    private void LoadInputFile()
    {
        var openFileDialog = new OpenFileDialog
            {
            FileName = "input.txt",
            Filter = "Text files (*.txt)|*.txt|All Files (*)|*",
            Title = "Open gifting file",
        };

        if (!openFileDialog.ShowDialog() ?? true)
            return;

        try
        {
            using (var str = openFileDialog.OpenFile())
            {
                ReadInputFile(str);
            }
        }
        catch (SecurityException ex)
        {
            MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                            $"Details:\n\n{ex.StackTrace}");
        }

        GeneratePendingAssignments();
        SetSeed();
        
        DraftRoundOptions.Clear();
        DraftRounds = 0;
        foreach (var i in Enumerable.Range(1, Categories.Count))
            DraftRoundOptions.Add(i);

        DraftRounds = 1;
        DraftNotStarted = true;
    }

    private void ReadInputFile(Stream str)
    {
        People.Clear();
        Participants.Clear();
        Categories.Clear();
        MadeAssignments.Clear();
        DraftOrder.Clear();
        DraftingParticipant = null;

        string line;
        var file = new StreamReader(new BufferedStream(str));

        while (true)
        {
            line = file.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                break;

            People.Add(new Person(line));
        }

        while (true)
        {
            line = file.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                break;

            Categories.Add(new Category(line));
        }

        foreach (var participant in People.Select(p => new Participant(p)))
        {
            Participants.Add(participant);
        }
    }

    private void SaveAssignments()
    {
        var now = DateTime.Now;
        var saveFileDialog = new SaveFileDialog
        {
            FileName = $"draftOutput_{now:yy-MM-dd_HH-mm}.txt",
            Filter = "Text Files (*.txt)|*.txt|All files (*)|*",
            Title = "Export Assignments",
        };

        if (!saveFileDialog.ShowDialog() ?? true)
            return;

        try
        {
            using (var str = saveFileDialog.OpenFile())
            {
                WriteOutputFile(str, now);
            }
        }
        catch (SecurityException ex)
        {
            MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                            $"Details:\n\n{ex.StackTrace}");
        }
    }

    private void WriteOutputFile(Stream str, DateTime now)
    {
        var writer = new StreamWriter(str);

        writer.WriteLine($"GiftDrafter Output {now}");
        writer.WriteLine($"Seed: \"{Seed}\", Draft Rounds: {DraftRounds}\n");
        writer.WriteLine("Assignments:");

        foreach (var participant in Participants)
        {
            writer.WriteLine("\n" + participant.Person.Name);
            foreach (var assignment in participant.Assignments)
            {
                writer.WriteLine(assignment.Display);
            }
        }

        writer.Flush();
    }

    #endregion

    private void GeneratePendingAssignments()
    {
        PendingAssignments.Clear();
        DraftingParticipant = null;

        foreach (var person in People)
        {
            foreach (var category in Categories)
            {
                PendingAssignments.Add(new Assignment(person, category));
            }
        }
    }

    private void GenerateDraftOrder()
    {
        var firstRound = DraftOrder.FirstOrDefault();
        firstRound?.PropertyChanged -= OnCurrentDrafterChanged;

        DraftOrder.Clear();
        if (Participants.Count == 0) return;

        var pList = Participants.ToList();
        var pListReverse = Participants.Reverse().ToList();

        if (Seed != "0")
            Rng.ShuffleList(pList);

        for (var i = 0; i < DraftRounds; i++)
        {
            var toAdd = i % 2 == 0 ? pList : pListReverse;
            DraftOrder.Add(new DraftRound(toAdd, i+1));
        }

        firstRound = DraftOrder.FirstOrDefault();
        if (firstRound is not null)
        {
            firstRound.PropertyChanged += OnCurrentDrafterChanged;
            firstRound.IsActive = true;
            DraftingParticipant = firstRound.Participants.FirstOrDefault();
        }

        UpdateAssignmentValidity();
    }

    private void GenerateRemainingGraph()
    {
        DraftOrder.Clear();
        var result = this.ConstructRemainingGraph();

        if (!result)
        {
            MessageBox.Show("Failed to construct gift assignment graph. Could not find valid permutation", "Error");
        }
    }

    private void OnCurrentDrafterChanged(object sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DraftRound.CurrentParticipant))
            UpdateAssignmentValidity();
    }

    private void UpdateAssignmentValidity()
    {
        foreach (var assignment in PendingAssignments)
            assignment.IsValid = DraftingParticipant?.CanBeAssigned(assignment) ?? false;
    }
}