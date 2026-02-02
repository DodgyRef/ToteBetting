using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExactaBetting.App.Services;
using ExactaBetting.Core.Models;
using ExactaBetting.Core.Services;

namespace ExactaBetting.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ValueBetService _valueBetService;
    private readonly SpreadsheetExportService _spreadsheetExport;

    /// <summary>Per-race (and per-settings) cache. Invalidated when Load Races is pressed.</summary>
    private bool _cacheInvalidatedByLoadRaces;
    private readonly Dictionary<string, List<ValueBet>> _cacheByKey = new();
    private readonly Dictionary<string, List<TrifectaValueBet>> _cacheTrifectaByKey = new();

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
    private ObservableCollection<TrifectaValueBet> _trifectaValueBets = [];

    [ObservableProperty]
    private ObservableCollection<TrifectaValueBet> _allTrifectaCalculations = [];

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

    public MainViewModel(ValueBetService valueBetService, SpreadsheetExportService spreadsheetExport)
    {
        _valueBetService = valueBetService;
        _spreadsheetExport = spreadsheetExport;
    }

    [RelayCommand]
    private async Task LoadRacesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = "Loading races...";
        _cacheInvalidatedByLoadRaces = true;
        _cacheByKey.Clear();
        _cacheTrifectaByKey.Clear();
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
        if (!_cacheInvalidatedByLoadRaces && _cacheByKey.TryGetValue(key, out var cached) && _cacheTrifectaByKey.TryGetValue(key, out var cachedTrifecta))
        {
            ApplyCachedResults(cached);
            ApplyTrifectaCachedResults(cachedTrifecta);
            await ExportSpreadsheetAndNavigateAsync(cached, cachedTrifecta);
            return;
        }

        IsBusy = true;
        StatusMessage = "Analyzing value bets...";
        ValueBets.Clear();
        AllValueCalculations.Clear();
        TrifectaValueBets.Clear();
        AllTrifectaCalculations.Clear();
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
            var allTrifecta = (await _valueBetService.GetAllTrifectaValueCalculationsAsync(SelectedRace, settings)).ToList();

            _cacheByKey[key] = all;
            _cacheTrifectaByKey[key] = allTrifecta;
            _cacheInvalidatedByLoadRaces = false;

            ApplyCachedResults(all);
            ApplyTrifectaCachedResults(allTrifecta);

            var topCount = all.Count(b => b.ValuePercent >= ValueThresholdPercent);
            var shown = Math.Min(topCount, TopBetCount);
            var trifectaTopCount = allTrifecta.Count(b => b.ValuePercent >= ValueThresholdPercent);
            var trifectaShown = Math.Min(trifectaTopCount, TopBetCount);
            StatusMessage = shown > 0 || trifectaShown > 0
                ? $"Found {shown} Exacta, {trifectaShown} Trifecta value bet(s) for {SelectedRace}."
                : $"No value bets above {ValueThresholdPercent}% threshold for {SelectedRace}. {all.Count} Exacta, {allTrifecta.Count} Trifecta combinations.";
            await ExportSpreadsheetAndNavigateAsync(all, allTrifecta);
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

    private async Task ExportSpreadsheetAndNavigateAsync(List<ValueBet> allExacta, List<TrifectaValueBet> allTrifecta)
    {
        if (string.IsNullOrEmpty(SelectedRace)) return;

        var raceData = await _valueBetService.GetRaceDataAsync(SelectedRace);
        if (raceData != null)
        {
            var path = _spreadsheetExport.ExportToSpreadsheet(SelectedRace, raceData, allExacta, allTrifecta);
            if (!string.IsNullOrEmpty(path))
                StatusMessage = (StatusMessage?.TrimEnd() ?? "") + $"  Spreadsheet: {path}";
        }

        var nav = new Dictionary<string, object>
        {
            ["ValueBets"] = ValueBets.ToList(),
            ["TrifectaValueBets"] = TrifectaValueBets.ToList(),
            ["AllValueCalculations"] = AllValueCalculations.ToList(),
            ["AllTrifectaCalculations"] = AllTrifectaCalculations.ToList(),
            ["RaceName"] = SelectedRace ?? ""
        };
        await Shell.Current.GoToAsync("ValueBetsResults", nav);
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

    private void ApplyTrifectaCachedResults(List<TrifectaValueBet> all)
    {
        TrifectaValueBets.Clear();
        AllTrifectaCalculations.Clear();
        foreach (var b in all)
            AllTrifectaCalculations.Add(b);
        var top = all
            .Where(b => b.ValuePercent >= ValueThresholdPercent)
            .OrderByDescending(b => b.ValuePercent)
            .Take(TopBetCount)
            .ToList();
        foreach (var b in top)
            TrifectaValueBets.Add(b);
    }

    [RelayCommand]
    private async Task OpenAllValueCalculationsAsync()
    {
        var bets = AllValueCalculations.ToList();
        await Shell.Current.GoToAsync("AllValueCalculations", new Dictionary<string, object> { ["Bets"] = bets });
    }

    [RelayCommand]
    private async Task OpenAllTrifectaCalculationsAsync()
    {
        var bets = AllTrifectaCalculations.ToList();
        await Shell.Current.GoToAsync("AllTrifectaCalculations", new Dictionary<string, object> { ["Bets"] = bets });
    }
}
