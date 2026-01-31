using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExactaBetting.Core.Models;
using ExactaBetting.Core.Services;

namespace ExactaBetting.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ValueBetService _valueBetService;

    [ObservableProperty]
    private ObservableCollection<string> _availableRaces = [];

    [ObservableProperty]
    private string? _selectedRace;

    [ObservableProperty]
    private ObservableCollection<ValueBet> _valueBets = [];

    [ObservableProperty]
    private ObservableCollection<ValueBet> _allValueCalculations = [];

    [ObservableProperty]
    private string _statusMessage = "Select a race and tap Refresh to analyze value bets.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private decimal _valueThresholdPercent = 10m;

    [ObservableProperty]
    private decimal _minimumPoolSize = 5000m;

    [ObservableProperty]
    private decimal _defaultStake = 100m;

    [ObservableProperty]
    private int _topBetCount = 5;

    public MainViewModel(ValueBetService valueBetService)
    {
        _valueBetService = valueBetService;
    }

    [RelayCommand]
    private async Task LoadRacesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = "Loading races...";
        try
        {
            var races = await _valueBetService.GetAvailableRacesAsync();
            AvailableRaces.Clear();
            foreach (var r in races)
                AvailableRaces.Add(r);
            StatusMessage = races.Count > 0
                ? $"Loaded {races.Count} race(s). Select one and tap Refresh."
                : "No races with EXACTA data found. Check JSON files.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshValueBetsAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(SelectedRace)) return;
        IsBusy = true;
        StatusMessage = "Analyzing value bets...";
        ValueBets.Clear();
        AllValueCalculations.Clear();
        try
        {
            var settings = new ValueBetSettings
            {
                ValueThresholdPercent = ValueThresholdPercent,
                MinimumPoolSize = MinimumPoolSize,
                DefaultStakeForDilution = DefaultStake,
                TopBetCount = TopBetCount
            };
            var all = await _valueBetService.GetAllValueCalculationsAsync(SelectedRace, settings);
            foreach (var b in all)
                AllValueCalculations.Add(b);
            var top = all
                .Where(b => b.ValuePercent >= ValueThresholdPercent)
                .OrderByDescending(b => b.ValuePercent)
                .Take(TopBetCount)
                .ToList();
            foreach (var b in top)
                ValueBets.Add(b);
            StatusMessage = top.Count > 0
                ? $"Found {top.Count} value bet(s) for {SelectedRace}. {all.Count} total combinations."
                : $"No value bets above {ValueThresholdPercent}% threshold for {SelectedRace}. {all.Count} total combinations.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenAllValueCalculationsAsync()
    {
        if (AllValueCalculations.Count == 0) return;
        var bets = AllValueCalculations.ToList();
        await Shell.Current.GoToAsync("AllValueCalculations", new Dictionary<string, object> { ["Bets"] = bets });
    }
}
