using System;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace GiftDrafter;

public class MainWindowViewModel : ObservableObject
{
    private readonly Random _secretRng = new();

    public MainWindowViewModel()
    {
        OpenInputFileCommand = new RelayCommand(LoadInputFile);
        GenerateSeedCommand = new RelayCommand(GenerateSeed);
        SetSeedCommand = new RelayCommand(SetSeed);
        StartDraftCommand = new RelayCommand(StartDraft);
        DraftPairCommand = new RelayCommand(DraftPair);
        UnDraftAssignmentCommand = new RelayCommand<Assignment>(UnDraftAssignment);
        ChooseAtRandomCommand = new RelayCommand(ChooseAtRandom);
        SaveAssignmentsCommand = new RelayCommand(SaveAssignments);
        GenerateRemainingCommand = new RelayCommand(GenerateRemainingGraph);

        SelectedStrictness = new MediumStrictness();
        StrictnessOptions = [new MinimumStrictness(), new LowStrictness(), SelectedStrictness, new HighStrictness()];

        SetSeed();
    }


    public ObservableCollection<Participant> Participants { get; } = [];
    public ObservableCollection<Person> People { get; } = [];
    public ObservableCollection<Category> Categories { get; } = [];
    public ObservableCollection<DraftRound> DraftOrder { get; } = [];
    public ObservableCollection<Assignment> PendingAssignments { get; } = [];
    public ObservableCollection<Assignment> MadeAssignments { get; } = [];
    public ObservableCollection<int> DraftRoundOptions { get; } = [1];
    public ObservableCollection<Strictness> StrictnessOptions { get; }
    
    public ICommand OpenInputFileCommand { get; }
    public ICommand GenerateSeedCommand { get; }
    public ICommand SetSeedCommand { get; }
    public ICommand StartDraftCommand { get; }

    public ICommand DraftPairCommand { get; }
    public ICommand ChooseAtRandomCommand { get; }
    public ICommand UnDraftAssignmentCommand { get; }

    public ICommand GenerateRemainingCommand { get; }
    public ICommand SaveAssignmentsCommand { get; }


    public Participant DraftingParticipant
    {
        get => DraftOrder.FirstOrDefault()?.CurrentParticipant;
        set
        {
            var drafting = DraftOrder.FirstOrDefault();
            if (drafting is null)
                return;

            drafting.CurrentParticipant = value;
            UpdateAssignmentValidity();
        }
    }

    public Assignment SelectedAssignment { get; set => SetProperty(ref field, value); }
    public Strictness SelectedStrictness { get; set => SetProperty(ref field, value); }

    public Random Rng { get; set; }
    public string Seed { get; set => SetProperty(ref field, value); } = "0";

    public bool DraftStarted
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
                RaisePropertyChanged(nameof(DraftNotStarted));
        }
    }

    public bool DraftNotStarted => !DraftStarted;

    public int DraftRounds
    {
        get;
        set
        {
            SetProperty(ref field, value);
            GenerateDraftOrder();
        }
    } = 1;


    #region Draft Commands

    private void GenerateSeed()
    {
        Seed = _secretRng.Next().ToString();
        SetSeed();
    }


    private void SetSeed()
    {
        if (!int.TryParse(Seed, out var seedInt))
            seedInt = Seed.GetHashCode();

        Rng = new Random(seedInt);
        GenerateDraftOrder();
    }


    private void StartDraft()
    {
        DraftStarted = true;
    }


    private void DraftPair()
    {
        var draftRound = DraftOrder.FirstOrDefault();

        if (DraftingParticipant is null || !(SelectedAssignment?.IsValid ?? false) || draftRound is null)
            return;

        var assignment = SelectedAssignment;
        PendingAssignments.Remove(assignment);
        MadeAssignments.Add(assignment);
        assignment.Giver = DraftingParticipant.Person;
        DraftingParticipant.Assignments.Add(assignment);
        draftRound.MadeAssignments.Add(assignment);
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
        if (validChoices.Length == 0) return;
        SelectedAssignment = validChoices[Rng.Next(validChoices.Length)];
    }


    private void UnDraftAssignment(Assignment assignment)
    {
        var giverPerson = assignment.Giver;
        var giverParticipant = Participants.FirstOrDefault(p => p.Person == giverPerson);
        var draftRound = DraftOrder.FirstOrDefault();
        
        if (giverPerson is null || giverParticipant is null)
            return;

        giverParticipant.Assignments.Remove(assignment);
        MadeAssignments.Remove(assignment);
        PendingAssignments.Add(assignment);
        assignment.Giver = null;

        if (draftRound is null)
        {
            draftRound = new DraftRound([giverParticipant], DraftRounds) { IsActive = true };
            draftRound.PropertyChanged += OnCurrentDrafterChanged;
            DraftOrder.Add(draftRound);
        }
        else
        {
            draftRound.Participants.Insert(0, giverParticipant);
        }

        draftRound.CurrentParticipant = giverParticipant;

        UpdateAssignmentValidity();
    }

    #endregion

    #region FileAccess

    private void LoadInputFile()
    {
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        {
            Reset();
            Initialize();
            return;
        }

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

        Initialize();
    }


    private void ReadInputFile(Stream str)
    {
        Reset();
        
        People.Clear();
        Participants.Clear();
        Categories.Clear();

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
        
        var output = ConstructOutput(now);

        MessageBox.Show(output, "Output");

        if (!saveFileDialog.ShowDialog() ?? true)
            return;
        
        try
        {
            using (var str = saveFileDialog.OpenFile())
            {
                var writer = new StreamWriter(str);
                writer.Write(output);
                writer.Flush();
            }
        }
        catch (SecurityException ex)
        {
            MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                            $"Details:\n\n{ex.StackTrace}");
        }
    }


    private string ConstructOutput(DateTime now)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"GiftDrafter Output {now}");
        sb.AppendLine($"Seed: \"{Seed}\", Draft Rounds: {DraftRounds}, Strictness: {SelectedStrictness.DisplayName}\n");
        sb.AppendLine("Assignments:");

        foreach (var participant in Participants)
        {
            sb.AppendLine("\n" + participant.Person.Name);
            foreach (var assignment in participant.Assignments)
            {
                sb.AppendLine(assignment.Display);
            }
        }

        return sb.ToString();
    }

    #endregion


    private void Initialize()
    {
        GeneratePendingAssignments();
        SetSeed();
        
        DraftRoundOptions.Clear();
        DraftRounds = 0;
        foreach (var i in Enumerable.Range(1, Categories.Count))
            DraftRoundOptions.Add(i);

        DraftRounds = 1;
        DraftStarted = false;
    }


    private void Reset()
    {
        foreach (var participant in Participants)
            participant.Assignments.Clear();

        foreach (var assignment in PendingAssignments.Concat(MadeAssignments))
            assignment.Giver = null;

        PendingAssignments.Clear();
        MadeAssignments.Clear();
        DraftOrder.Clear();
        DraftingParticipant = null;
    }


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
        var result = this.ConstructRemainingGraph();

        if (!result)
        {
            MessageBox.Show("Failed to construct gift assignment graph. Could not find valid permutation", "Error");
            return;
        }

        DraftOrder.Clear();
        DraftStarted = true;
        foreach (var pendingAssignment in PendingAssignments)
            MadeAssignments.Add(pendingAssignment);

        PendingAssignments.Clear();

        foreach (var participant in Participants)
            foreach (var assignment in participant.Assignments)
                assignment.Giver = participant.Person;
    }


    private void OnCurrentDrafterChanged(object sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DraftRound.CurrentParticipant))
            UpdateAssignmentValidity();
    }


    private void UpdateAssignmentValidity()
    {
        foreach (var assignment in PendingAssignments)
        {
            assignment.IsValid = SelectedStrictness.CanDraft(DraftingParticipant, assignment, DraftOrder.FirstOrDefault());

        }
    }
}