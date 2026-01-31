using ExactaBetting.App.ViewModels;
using ExactaBetting.Core.Models;

namespace ExactaBetting.App.Views;

public partial class AllValueCalculationsPage : ContentPage, IQueryAttributable
{
    public AllValueCalculationsPage()
    {
        InitializeComponent();
        BindingContext = new AllValueCalculationsViewModel();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Bets", out var betsObj) && betsObj is IEnumerable<ValueBet> bets && BindingContext is AllValueCalculationsViewModel vm)
            vm.LoadBets(bets);
    }
}
