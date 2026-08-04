using EnterpriseIdentityService.Application.Abstractions;

namespace EnterpriseIdentityService.UnitTests.Application.Abstractions;

public sealed class ResultTests
{
    private static readonly Error TestError = new("Test.Error", "A test error.");

    [Fact]
    public void Success_ShouldReportSuccessAndNoError()
    {
        Result result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_ShouldReportFailureAndPreserveError()
    {
        Result result = Result.Failure(TestError);

        Assert.True(result.IsFailure);
        Assert.Equal(TestError, result.Error);
    }

    [Fact]
    public void TypedSuccess_ShouldExposeValue()
    {
        Result<int> result = Result<int>.Success(42);

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void TypedFailure_ShouldThrowWhenValueIsAccessed()
    {
        Result<int> result = Result<int>.Failure(TestError);

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_ShouldRejectNoError()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(Error.None));
    }

    [Fact]
    public void Failure_ShouldRejectNullError()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
    }

    [Theory]
    [InlineData("", "A description.")]
    [InlineData("Test.Error", "")]
    public void Failure_ShouldRejectIncompleteError(string code, string description)
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(new Error(code, description)));
    }
}
