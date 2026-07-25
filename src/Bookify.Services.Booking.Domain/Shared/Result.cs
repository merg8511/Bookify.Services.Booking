namespace Bookify.Services.Booking.Domain.Shared;

public class Result
{
    protected Result(
        bool isSuccess,
        Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException(
                "A successful result cannot contain an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException(
                "A failed result must contain an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() =>
        new(
            isSuccess: true,
            Error.None);

    public static Result Failure(Error error) =>
        new(
            isSuccess: false,
            error);
}
