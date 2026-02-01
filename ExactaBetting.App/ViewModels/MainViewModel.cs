using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExactaBetting.Core.Models;
using ExactaBetting.Core.Services;

namespace ExactaBetting.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ValueBetService _valueBetService;

    /// <summary>Per-race (and per-settings) cache. Invalidated when Load Races is pressed.</summary>
    private bool _cacheInvalidatedByLoadRaces;
    private readonly Dictionary<string, List<ValueBet>> _cacheByKey = new();

    /// <summary>All races from API (with display name and country). Filtered by CountryFilter into FilteredRaces.</summary>
    [ObservableProperty]
    private ObservableCollection<AvailableRace> _availableRaces = [];

    /// <summary>Races shown in the dropdown (filtered by country).</summary>
    [ObservableProperty]
    private ObservableCollection<AvailableRace> _filteredRaces = [];

    /// <summary>Country filter: All, UK & Ireland, International.</summary>
    [ObservableProperty]
    private string _countryFilter = "All";

    /// <summary>Options for the country filter Picker.</summary>
    public IReadOnlyList<string> CountryFilterOptions { get; } = ["All", "UK & Ireland", "International"];

    partial void OnCountryFilterChanged(string value) => ApplyCountryFilter();

    [ObservableProperty]
    private AvailableRace? _selectedRaceOption;

    partial void OnSelectedRaceOptionChanged(AvailableRace? value)
    {
        SelectedRace = value?.BaseName;
    }

    /// <summary>Base race name for API lookups (from SelectedRaceOption).</summary>
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
        _cacheInvalidatedByLoadRaces = true;
        _cacheByKey.Clear();
        _valueBetService.InvalidateRaceDataCache();
        try
        {
            var races = await _valueBetService.GetAvailableRacesAsync();
            AvailableRaces.Clear();
            foreach (var r in races)
                AvailableRaces.Add(r);
            ApplyCountryFilter();
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

    private static string CacheKey(string race, decimal valueThreshold, decimal minPool, decimal stake, int topCount)
        => $"{race}|{valueThreshold}|{minPool}|{stake}|{topCount}";

    private void ApplyCountryFilter()
    {
        FilteredRaces.Clear();
        var code = (CountryFilter ?? "All").Trim();
        foreach (var r in AvailableRaces)
        {
            var cc = (r.CountryCode ?? "").Trim().ToUpperInvariant();
            var include = code switch
            {
                "UK & Ireland" => cc is "UK" or "IE" or "GB",
                "International" => cc is not "" and not "UK" and not "IE" and not "GB",
                _ => true // All
            };
            if (include)
                FilteredRaces.Add(r);
        }
        if (SelectedRaceOption is { } sel && !FilteredRaces.Any(r => r.BaseName == sel.BaseName))
            SelectedRaceOption = null;
    }

    [RelayCommand]
    private async Task RefreshValueBetsAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(SelectedRace)) return;

        var key = CacheKey(SelectedRace, ValueThresholdPercent, MinimumPoolSize, DefaultStake, TopBetCount);
        if (!_cacheInvalidatedByLoadRaces && _cacheByKey.TryGetValue(key, out var cached))
        {
            ApplyCachedResults(cached);
            StatusMessage = $"Loaded from cache ({ValueBets.Count} value bet(s), {AllValueCalculations.Count} total).";
            return;
        }

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
            var all = (await _valueBetService.GetAllValueCalculationsAsync(SelectedRace, settings)).ToList();

            _cacheByKey[key] = all;
            _cacheInvalidatedByLoadRaces = false;

            ApplyCachedResults(all);
            var topCount = all.Count(b => b.ValuePercent >= ValueThresholdPercent);
            var shown = Math.Min(topCount, TopBetCount);
            StatusMessage = shown > 0
                ? $"Found {shown} value bet(s) for {SelectedRace}. {all.Count} total combinations."
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

    private void ApplyCachedResults(List<ValueBet> all)
    {
        ValueBets.Clear();
        AllValueCalculations.Clear();
        foreach (var b in all)
            AllValueCalculations.Add(b);
        var top = all
            .Where(b => b.ValuePercent >= ValueThresholdPercent)
            .OrderByDescending(b => b.ValuePercent)
            .Take(TopBetCount)
            .ToList();
        foreach (var b in top)
            ValueBets.Add(b);
    }

    [RelayCommand]
    private async Task OpenAllValueCalculationsAsync()
    {
        var bets = AllValueCalculations.ToList();
        await Shell.Current.GoToAsync("AllValueCalculations", new Dictionary<string, object> { ["Bets"] = bets });
    }
}
