namespace Bookify.Services.Booking.Application.Abstractions.Messaging;

public interface ICommand;
public interface ICommand<TResponse> : ICommand;
