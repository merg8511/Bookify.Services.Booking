namespace Bookify.Services.Booking.Domain.Shared;

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(
        T? value,
        bool isSuccess,
        Error error
        ) : base(isSuccess, error)
    {
        _value = value;
    }

    public T Value =>
        IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            "The value of a failed result cannot be accessed.");

    public static Result<T> Success(T value) =>
        new(
            value,
            isSuccess: true,
            Error.None);

    public new static Result<T> Failure(Error error) =>
        new(default,
            isSuccess: false,
            error);
}
