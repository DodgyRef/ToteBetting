using ExactaBetting.App.ViewModels;
using ExactaBetting.Core.Models;

namespace ExactaBetting.App.Views;

public partial class ValueBetsResultsPage : ContentPage, IQueryAttributable
{
    public ValueBetsResultsPage()
    {
        InitializeComponent();
        BindingContext = new ValueBetsResultsViewModel();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not ValueBetsResultsViewModel vm) return;

        var valueBets = query.TryGetValue("ValueBets", out var vb) && vb is IEnumerable<ValueBet> vbl ? vbl : [];
        var trifectaValueBets = query.TryGetValue("TrifectaValueBets", out var tvb) && tvb is IEnumerable<TrifectaValueBet> tvbl ? tvbl : [];
        var allValueCalculations = query.TryGetValue("AllValueCalculations", out var avc) && avc is IEnumerable<ValueBet> avcl ? avcl : [];
        var allTrifectaCalculations = query.TryGetValue("AllTrifectaCalculations", out var atc) && atc is IEnumerable<TrifectaValueBet> atcl ? atcl : [];
        var raceName = query.TryGetValue("RaceName", out var rn) ? rn as string : null;

        vm.Load(valueBets, trifectaValueBets, allValueCalculations, allTrifectaCalculations, raceName);
    }
}
