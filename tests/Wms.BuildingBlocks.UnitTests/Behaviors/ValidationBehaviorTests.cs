using FluentValidation;
using Wms.BuildingBlocks.Application.Behaviors;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.BuildingBlocks.UnitTests.Behaviors;

// What: unit test ValidationBehavior — no-throw-for-business (ADR-0019)
// Why: input invalid harus jadi Result(Validation) yang short-circuit SEBELUM handler, BUKAN
// exception. Juga membuktikan ResultFactory menyusun failure ber-tipe Result<T> via refleksi.
public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Invalid_request_short_circuits_with_validation_result_without_throwing()
    {
        var behavior = new ValidationBehavior<TestCommand, Result<Guid>>([new TestCommandValidator()]);
        var handlerInvoked = false;

        var response = await behavior.Handle(
            new TestCommand(string.Empty),
            _ =>
            {
                handlerInvoked = true;
                return Task.FromResult(Result.Success(Guid.NewGuid()));
            },
            CancellationToken.None);

        Assert.True(response.IsFailure);
        Assert.Equal(ErrorType.Validation, response.Error.Type);
        Assert.False(handlerInvoked);   // short-circuit: handler tak dipanggil
    }

    [Fact]
    public async Task Valid_request_passes_through_to_handler()
    {
        var behavior = new ValidationBehavior<TestCommand, Result<Guid>>([new TestCommandValidator()]);
        var expected = Guid.NewGuid();

        var response = await behavior.Handle(
            new TestCommand("ok"), _ => Task.FromResult(Result.Success(expected)), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(expected, response.Value);
    }

    [Fact]
    public async Task No_validators_registered_passes_through()
    {
        var behavior = new ValidationBehavior<TestCommand, Result<Guid>>([]);

        var response = await behavior.Handle(
            new TestCommand(string.Empty),
            _ => Task.FromResult(Result.Success(Guid.NewGuid())),
            CancellationToken.None);

        Assert.True(response.IsSuccess);
    }

    private sealed record TestCommand(string Name) : ICommand<Guid>;

    private sealed class TestCommandValidator : AbstractValidator<TestCommand>
    {
        public TestCommandValidator() => RuleFor(command => command.Name).NotEmpty();
    }
}
