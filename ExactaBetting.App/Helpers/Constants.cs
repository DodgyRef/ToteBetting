using System;

namespace ToteBettingApp.Helpers;

/// <summary>
/// Central place for API endpoints and configuration values.
/// </summary>
public static class Constants
{
    public const string ProductionEndpoint = "https://hub.production.racing.tote.co.uk/partner/gateway/graphql";
    // Test endpoint per Tote documentation: https://developers.services.tote.co.uk/guides/test-cards/
    public const string TestEndpoint = "https://playground.edge.tote.digital/partner/gateway/test/graphql/";
    public const string SubscriptionEndpoint = "wss://hub.production.racing.tote.co.uk/partner/gateway/graphql";
    // Test subscription endpoint per Tote documentation
    public const string TestSubscriptionEndpoint = "https://playground.edge.tote.digital/partner/connections/test/graphql/";

    public static Uri GetEndpoint(bool useTest) => new(useTest ? TestEndpoint : ProductionEndpoint);
}
