using FluentAssertions;
using SonrisaNews.Domain;
using Xunit;

namespace SonrisaNews.UnitTests;

public class DomainExceptionTests
{
    [Fact]
    public void Constructor_SetsCodeAndMessage()
    {
        var ex = new DomainException("AlertNotFound", "The alert was not found.");

        ex.Code.Should().Be("AlertNotFound");
        ex.Message.Should().Be("The alert was not found.");
    }

    [Fact]
    public void DomainException_IsAnException()
    {
        // Domain exceptions must be catchable as System.Exception so middleware
        // can route them through the ProblemDetails pipeline.
        var ex = new DomainException("X", "x");
        ex.Should().BeAssignableTo<Exception>();
    }
}
