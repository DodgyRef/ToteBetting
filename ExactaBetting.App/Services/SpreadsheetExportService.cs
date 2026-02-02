using System.Globalization;
using ClosedXML.Excel;
using ExactaBetting.Core.Models;

namespace ExactaBetting.App.Services;

/// <summary>
/// Builds an Excel spreadsheet for the selected race with WIN, EXACTA and TRIFECTA sheets
/// matching the layout of ExampleCalculationSpreadsheet.xlsx.
/// </summary>
public sealed class SpreadsheetExportService
{
    /// <summary>
    /// Exports race data and value calculations to an xlsx file in Documents\Tote Betting.
    /// Filename: {RaceNameNormalized}_{yyyyMMdd_HHmmss}.xlsx
    /// </summary>
    /// <param name="baseRaceName">Race name from dropdown (no pool text), e.g. "SOUTHWELL RACE 2 13:37".</param>
    /// <param name="raceData">Race data including WIN pool and odds.</param>
    /// <param name="allExactaCalculations">All exacta value calculations for the race.</param>
    /// <param name="allTrifectaCalculations">All trifecta value calculations for the race.</param>
    /// <returns>Full path of the saved file, or null if export failed.</returns>
    public string? ExportToSpreadsheet(
        string baseRaceName,
        RaceData raceData,
        IReadOnlyList<ValueBet> allExactaCalculations,
        IReadOnlyList<TrifectaValueBet> allTrifectaCalculations)
    {
        var fileName = BuildFileName(baseRaceName);
        var directory = GetExportDirectory();
        if (string.IsNullOrEmpty(directory))
            return null;

        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, fileName);

        using var workbook = new XLWorkbook();

        // Sheet 1: WIN (layout matches example)
        var winSheetName = SanitizeSheetName($"{baseRaceName} - WIN");
        var winSheet = workbook.Worksheets.Add(winSheetName);
        WriteWinSheet(winSheet, baseRaceName, raceData);

        // Sheet 2: EXACTA
        var exactaSheetName = SanitizeSheetName($"{baseRaceName} - EXACTA");
        var exactaSheet = workbook.Worksheets.Add(exactaSheetName);
        WriteExactaSheet(exactaSheet, raceData, allExactaCalculations);

        // Sheet 3: TRIFECTA
        var trifectaSheetName = SanitizeSheetName($"{baseRaceName} - TRIFECTA");
        var trifectaSheet = workbook.Worksheets.Add(trifectaSheetName);
        WriteTrifectaSheet(trifectaSheet, raceData, allTrifectaCalculations);

        workbook.SaveAs(filePath);
        return filePath;
    }

    private static string BuildFileName(string baseRaceName)
    {
        var normalized = baseRaceName
            .Replace(" ", "_", StringComparison.Ordinal)
            .Replace(":", "", StringComparison.Ordinal);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return $"{normalized}_{timestamp}.xlsx";
    }

    private static string GetExportDirectory()
    {
        try
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return string.IsNullOrEmpty(documents) ? "" : Path.Combine(documents, "Tote Betting");
        }
        catch
        {
            return "";
        }
    }

    private static string SanitizeSheetName(string name)
    {
        const int maxLen = 31;
        var sanitized = new string(name.Where(c => c != '\\' && c != '/' && c != '*' && c != '?' && c != '[' && c != ']').ToArray());
        return sanitized.Length <= maxLen ? sanitized : sanitized[..maxLen];
    }

    private static void WriteWinSheet(IXLWorksheet sheet, string raceName, RaceData race)
    {
        // Headers (match example: id, Horse Number, legs, odds, 1/Odds, Amount Bet On Horse, pool total grossAmount, pool total netAmount, pool carryIn, pool guarantee, pool topUp, selling status)
        sheet.Cell(1, 1).Value = "id";
        sheet.Cell(1, 2).Value = "Horse Number";
        sheet.Cell(1, 3).Value = "legs";
        sheet.Cell(1, 4).Value = "odds";
        sheet.Cell(1, 5).Value = "1/Odds";
        sheet.Cell(1, 6).Value = "Amount Bet On Horse";
        sheet.Cell(1, 7).Value = "pool total grossAmount";
        sheet.Cell(1, 8).Value = "pool total netAmount";
        sheet.Cell(1, 9).Value = "pool carryIn netAmount";
        sheet.Cell(1, 10).Value = "pool guarantee netAmount";
        sheet.Cell(1, 11).Value = "pool topUp netAmount";
        sheet.Cell(1, 12).Value = "selling status";

        // Amount bet on each horse = IF(Odds=0, 0, PoolTotalGrossAmount / (Odds * SUM(1/Odds)))
        var poolNet = race.WinPoolNetAmount;
        var sumRecipOdds = 0m;
        foreach (var kvp in race.WinOdds.OrderBy(k => k.Key))
            sumRecipOdds += kvp.Value > 0 ? 1m / kvp.Value : 0m;

        var row = 2;
        foreach (var kvp in race.WinOdds.OrderBy(k => k.Key))
        {
            var horseNum = kvp.Key;
            var odds = kvp.Value;
            var name = race.HorseNames.TryGetValue(horseNum, out var n) ? n : $"#{horseNum}";
            var id = $"WIN-{raceName.Replace(" ", "-", StringComparison.Ordinal)}-{horseNum}";

            sheet.Cell(row, 1).Value = id;
            sheet.Cell(row, 2).Value = horseNum;
            sheet.Cell(row, 3).Value = name;
            sheet.Cell(row, 4).Value = (double)odds;
            sheet.Cell(row, 5).Value = odds > 0 ? (double)(1m / odds) : 0;
            sheet.Cell(row, 6).Value = (double)((poolNet)/ odds);
            sheet.Cell(row, 7).Value = (double)race.WinPoolGrossAmount;
            sheet.Cell(row, 8).Value = (double)race.WinPoolNetAmount;
            sheet.Cell(row, 9).Value = (double)race.WinCarryInNetAmount;
            sheet.Cell(row, 10).Value = (double)race.WinGuaranteeNetAmount;
            sheet.Cell(row, 11).Value = (double)race.WinTopUpNetAmount;
            sheet.Cell(row, 12).Value = "CLOSED";
            row++;
        }
    }

    /// <summary>Amount bet on each combination/horse = IF(Odds=0, 0, PoolTotalGrossAmount / (ToteOddsForCombination * SUM(1/Odds))).</summary>
    private static decimal GetAmountBetOnCombination(decimal poolGross, decimal toteOddsForCombination, decimal sumRecipToteOdds)
    {
        if (toteOddsForCombination <= 0 || sumRecipToteOdds <= 0 || poolGross <= 0) return 0m;
        return poolGross / (toteOddsForCombination * sumRecipToteOdds);
    }

    private static void WriteExactaSheet(IXLWorksheet sheet, RaceData race, IReadOnlyList<ValueBet> all)
    {
        // Headers: Horse First, Horse Second, Tote Odds, ..., Value %, Pool Total, Amount Bet On Combination
        sheet.Cell(1, 1).Value = "Horse First";
        sheet.Cell(1, 2).Value = "Horse Second";
        sheet.Cell(1, 3).Value = "Tote Odds";
        sheet.Cell(1, 4).Value = "Fair Exacta Probability";
        sheet.Cell(1, 5).Value = "Fair Odds H1";
        sheet.Cell(1, 6).Value = "Fair Odds H2";
        sheet.Cell(1, 7).Value = "pFirst";
        sheet.Cell(1, 8).Value = "pSecond";
        sheet.Cell(1, 9).Value = "pFirst Not Wins";
        sheet.Cell(1, 10).Value = "Fair Exacta Odds";
        sheet.Cell(1, 11).Value = "Value %";
        sheet.Cell(1, 12).Value = "Pool Total";
        sheet.Cell(1, 13).Value = "Amount Bet On Combination";

        var poolNet = race.PoolNetAmount;
        var sumRecipToteOdds = 0m;
        foreach (var b in all)
            sumRecipToteOdds += b.ToteOdds > 0 ? 1m / b.ToteOdds : 0m;

        var row = 2;
        foreach (var b in all)
        {
            var o1 = race.WinOdds.TryGetValue(b.First, out var oFirst) ? oFirst : 0m;
            var o2 = race.WinOdds.TryGetValue(b.Second, out var oSecond) ? oSecond : 0m;
            var pFirst = o1 > 0 ? 1m / o1 : 0m;
            var pSecond = o2 > 0 ? 1m / o2 : 0m;
            var pFirstNotWins = 1m - pFirst;
            var fairProb = b.FairOdds > 0 ? 1m / b.FairOdds : 0m;

            sheet.Cell(row, 1).Value = b.First;
            sheet.Cell(row, 2).Value = b.Second;
            sheet.Cell(row, 3).Value = (double)b.ToteOdds;
            sheet.Cell(row, 4).Value = (double)fairProb;
            sheet.Cell(row, 5).Value = (double)o1;
            sheet.Cell(row, 6).Value = (double)o2;
            sheet.Cell(row, 7).Value = (double)pFirst;
            sheet.Cell(row, 8).Value = (double)pSecond;
            sheet.Cell(row, 9).Value = (double)pFirstNotWins;
            sheet.Cell(row, 10).Value = (double)b.FairOdds;
            sheet.Cell(row, 11).Value = (double)b.ValuePercent;
            sheet.Cell(row, 12).Value = (double)race.PoolNetAmount;
            sheet.Cell(row, 13).Value = (double)(poolNet/b.ToteOdds);
            row++;
        }
    }

    private static void WriteTrifectaSheet(IXLWorksheet sheet, RaceData race, IReadOnlyList<TrifectaValueBet> all)
    {
        // Headers: Horse First, Horse Second, Horse Third, Tote Odds, ..., Value %, Pool Total, Amount Bet On Combination
        sheet.Cell(1, 1).Value = "Horse First";
        sheet.Cell(1, 2).Value = "Horse Second";
        sheet.Cell(1, 3).Value = "Horse Third";
        sheet.Cell(1, 4).Value = "Tote Odds";
        sheet.Cell(1, 5).Value = "Fair Trifecta Probability";
        sheet.Cell(1, 6).Value = "Fair Odds H1";
        sheet.Cell(1, 7).Value = "Fair Odds H2";
        sheet.Cell(1, 8).Value = "Fair Odds H3";
        sheet.Cell(1, 9).Value = "pFirst";
        sheet.Cell(1, 10).Value = "pSecond";
        sheet.Cell(1, 11).Value = "pThird";
        sheet.Cell(1, 12).Value = "pFirst And Second";
        sheet.Cell(1, 13).Value = "Fair Trifecta Odds";
        sheet.Cell(1, 14).Value = "Value %";
        sheet.Cell(1, 15).Value = "Pool Total";
        sheet.Cell(1, 16).Value = "Amount Bet On Combination";

        var poolNet = race.TrifectaPoolNetAmount;
        var sumRecipToteOdds = 0m;
        foreach (var b in all)
            sumRecipToteOdds += b.ToteOdds > 0 ? 1m / b.ToteOdds : 0m;

        var row = 2;
        foreach (var b in all)
        {
            var o1 = race.WinOdds.TryGetValue(b.First, out var oFirst) ? oFirst : 0m;
            var o2 = race.WinOdds.TryGetValue(b.Second, out var oSecond) ? oSecond : 0m;
            var o3 = race.WinOdds.TryGetValue(b.Third, out var oThird) ? oThird : 0m;
            var pFirst = o1 > 0 ? 1m / o1 : 0m;
            var pSecond = o2 > 0 ? 1m / o2 : 0m;
            var pThird = o3 > 0 ? 1m / o3 : 0m;
            var pFirstAndSecond = 1m - pFirst - pSecond;
            var fairProb = b.FairOdds > 0 ? 1m / b.FairOdds : 0m;

            sheet.Cell(row, 1).Value = b.First;
            sheet.Cell(row, 2).Value = b.Second;
            sheet.Cell(row, 3).Value = b.Third;
            sheet.Cell(row, 4).Value = (double)b.ToteOdds;
            sheet.Cell(row, 5).Value = (double)fairProb;
            sheet.Cell(row, 6).Value = (double)o1;
            sheet.Cell(row, 7).Value = (double)o2;
            sheet.Cell(row, 8).Value = (double)o3;
            sheet.Cell(row, 9).Value = (double)pFirst;
            sheet.Cell(row, 10).Value = (double)pSecond;
            sheet.Cell(row, 11).Value = (double)pThird;
            sheet.Cell(row, 12).Value = (double)pFirstAndSecond;
            sheet.Cell(row, 13).Value = (double)b.FairOdds;
            sheet.Cell(row, 14).Value = (double)b.ValuePercent;
            sheet.Cell(row, 15).Value = (double)race.TrifectaPoolNetAmount;
            sheet.Cell(row, 16).Value = (double)(poolNet/b.ToteOdds);
            row++;
        }
    }
}
