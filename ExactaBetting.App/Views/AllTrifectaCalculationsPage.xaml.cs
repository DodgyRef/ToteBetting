using ExactaBetting.App.ViewModels;
using ExactaBetting.Core.Models;

namespace ExactaBetting.App.Views;

public partial class AllTrifectaCalculationsPage : ContentPage, IQueryAttributable
{
    public AllTrifectaCalculationsPage()
    {
        InitializeComponent();
        BindingContext = new AllTrifectaCalculationsViewModel();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Bets", out var betsObj) && betsObj is IEnumerable<TrifectaValueBet> bets && BindingContext is AllTrifectaCalculationsViewModel vm)
            vm.LoadBets(bets);
    }
}
