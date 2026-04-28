namespace TravSocialMediaAgent.Domain.Posts;

public sealed record TravSocialPost
{
    public TravSocialPost(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A social post message is required.", nameof(message));
        }

        Message = message.Trim();
    }

    public string Message { get; }

    public override string ToString() => Message;
}
