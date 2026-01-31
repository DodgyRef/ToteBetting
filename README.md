# EXACTA Value Betting

A cross-platform .NET MAUI application that analyzes TOTE Exacta odds to identify value bets. Runs on **Windows**, **macOS**, **iOS**, and **Android**.

## Features

- **Value calculation** from WIN pool odds using: P(A,B) ≈ P(A)×P(B)/(1−P(A))
- **Value percentage** = (Fair Odds / TOTE Odds − 1) × 100%
- **Pool dilution** adjustment for your stake
- **Configurable threshold** – only show bets above your minimum value %
- **Top 5 value bets** per race, ranked by value

## Architecture

```
ExactaBetting.App (MAUI)     → UI, ViewModels, Sample data service
ExactaBetting.Core           → Models, Value calculator, IToteApiService
```

### Replacing with Live Data

Implement `IToteApiService` in the Core project and register it in `MauiProgram.cs` instead of `SampleToteApiService`. Your implementation should:

1. Fetch WIN products (same race name) for implied probabilities
2. Fetch EXACTA products for live odds and pool totals
3. Map events to products using race name (e.g. `"KENILWORTH RACE 1"`)

Connect your GraphQL subscriptions to update data in real time.

## Running the App

### Windows
```bash
dotnet build ExactaBetting.App -f net10.0-windows10.0.19041.0
dotnet run --project ExactaBetting.App -f net10.0-windows10.0.19041.0
```

### macOS
```bash
dotnet build ExactaBetting.App -f net10.0-maccatalyst
dotnet run --project ExactaBetting.App -f net10.0-maccatalyst
```

### Android
```bash
dotnet build ExactaBetting.App -f net10.0-android
dotnet run --project ExactaBetting.App -f net10.0-android
```

## Sample Data

The app includes a `SampleToteApiService` that reads from a single file:
- `Example Responses/GetEXACTAProducts.json`

This file contains both WIN and EXACTA products. WIN product lines provide individual horse odds; EXACTA products provide combination odds and pool data. Races are matched by name (e.g. "KENILWORTH RACE 1"). When no matching WIN data exists for a race, synthetic WIN odds are used.

## Configuration

- **Value threshold (%)** – Minimum value to show (default: 10%)
- **Min pool size** – Filter out thin pools (default: 5,000)
- **Stake** – Used for dilution calculation (default: 100)
- **Top bets** – Number of value bets to display (default: 5)
