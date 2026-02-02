using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExactaBetting.Core.Models;

namespace ExactaBetting.App.ViewModels;

/// <summary>
/// View model for the Value Bets Results page. Shows Exacta and Trifecta value bets passed from MainViewModel.
/// </summary>
public partial class ValueBetsResultsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ValueBet> _valueBets = [];

    [ObservableProperty]
    private ObservableCollection<TrifectaValueBet> _trifectaValueBets = [];

    [ObservableProperty]
    private string _title = "Value Bets";

    private List<ValueBet> _allValueCalculations = [];
    private List<TrifectaValueBet> _allTrifectaCalculations = [];

    public void Load(
        IEnumerable<ValueBet> valueBets,
        IEnumerable<TrifectaValueBet> trifectaValueBets,
        IEnumerable<ValueBet> allValueCalculations,
        IEnumerable<TrifectaValueBet> allTrifectaCalculations,
        string? raceName = null)
    {
        ValueBets.Clear();
        foreach (var b in valueBets ?? [])
            ValueBets.Add(b);
        TrifectaValueBets.Clear();
        foreach (var b in trifectaValueBets ?? [])
            TrifectaValueBets.Add(b);
        _allValueCalculations = (allValueCalculations ?? []).ToList();
        _allTrifectaCalculations = (allTrifectaCalculations ?? []).ToList();
        Title = string.IsNullOrEmpty(raceName) ? "Value Bets" : $"Value Bets – {raceName}";
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task OpenAllValueCalculationsAsync()
    {
        var bets = _allValueCalculations.ToList();
        await Shell.Current.GoToAsync("AllValueCalculations", new Dictionary<string, object> { ["Bets"] = bets });
    }

    [RelayCommand]
    private async Task OpenAllTrifectaCalculationsAsync()
    {
        var bets = _allTrifectaCalculations.ToList();
        await Shell.Current.GoToAsync("AllTrifectaCalculations", new Dictionary<string, object> { ["Bets"] = bets });
    }
}
