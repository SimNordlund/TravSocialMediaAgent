namespace TravSocialMediaAgent.Infrastructure.Shared;

internal static class ConfigurationValue
{
    public static string? FirstNonWhiteSpace(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    public static string Require(string? value, string name)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        throw new InvalidOperationException($"{name} is required.");
    }
}
