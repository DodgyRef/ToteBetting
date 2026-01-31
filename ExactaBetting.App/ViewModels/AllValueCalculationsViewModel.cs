using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExactaBetting.Core.Models;

namespace ExactaBetting.App.ViewModels;

/// <summary>
/// View model for the All Value Calculations page. Holds the list passed via navigation.
/// </summary>
public partial class AllValueCalculationsViewModel : ObservableObject
{
    private List<ValueBet> _allBets = [];

    [ObservableProperty]
    private ObservableCollection<ValueBet> _bets = [];

    [ObservableProperty]
    private string _title = "All Value Calculations";

    [ObservableProperty]
    private ObservableCollection<string> _firstHorseFilterOptions = [];

    [ObservableProperty]
    private string _selectedFirstHorseFilterOption = "All";

    partial void OnSelectedFirstHorseFilterOptionChanged(string value)
    {
        ApplyFilter();
    }

    public void LoadBets(IEnumerable<ValueBet> bets)
    {
        _allBets = (bets ?? []).ToList();
        FirstHorseFilterOptions.Clear();
        FirstHorseFilterOptions.Add("All");
        foreach (var n in _allBets.Select(b => b.First).Distinct().OrderBy(i => i))
            FirstHorseFilterOptions.Add(n.ToString());
        SelectedFirstHorseFilterOption = "All";
        ApplyFilter();
        UpdateTitle();
    }

    private void ApplyFilter()
    {
        Bets.Clear();
        if (SelectedFirstHorseFilterOption == "All" || !int.TryParse(SelectedFirstHorseFilterOption, out var firstNum))
        {
            foreach (var b in _allBets)
                Bets.Add(b);
        }
        else
        {
            foreach (var b in _allBets.Where(b => b.First == firstNum))
                Bets.Add(b);
        }
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        Title = _allBets.Count == Bets.Count
            ? $"All Value Calculations ({Bets.Count})"
            : $"All Value Calculations ({Bets.Count} of {_allBets.Count})";
    }

    [RelayCommand]
    private void ResetFilter()
    {
        SelectedFirstHorseFilterOption = "All";
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
