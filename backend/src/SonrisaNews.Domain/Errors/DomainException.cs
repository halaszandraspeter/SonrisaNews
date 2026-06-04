namespace SonrisaNews.Domain;

/// <summary>Base type for exceptions that represent business-rule violations. Catch this in middleware; let <see cref="Exception"/> bubble up to 500s.</summary>
public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }
}
