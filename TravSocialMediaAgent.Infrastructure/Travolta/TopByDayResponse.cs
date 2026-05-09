using System.Text.Json.Serialization;

namespace TravSocialMediaAgent.Infrastructure.Travolta;

internal sealed class TopByDayResponse
{
    public bool Found { get; set; }

    public string? Date { get; set; }

    public string? Track { get; set; }

    public string? Form { get; set; }

    public List<RaceTop> Races { get; set; } = [];
}

internal sealed class RaceTop
{
    public int? Lopp { get; set; }

    public int? Avdelning { get; set; }

    public List<HorseTop> Top { get; set; } = [];

    [JsonIgnore]
    public int DisplayLopp => Lopp ?? Avdelning ?? 0;
}

internal sealed class HorseTop
{
    public int Rank { get; set; }

    public int Number { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Analysis { get; set; }
}
