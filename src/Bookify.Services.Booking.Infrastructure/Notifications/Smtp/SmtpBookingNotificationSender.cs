using System.IO;
using System.Net.Sockets;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Domain.Shared;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

using MailKitAuthenticationException = MailKit.Security.AuthenticationException;
using MailKitSslHandshakeException = MailKit.Security.SslHandshakeException;

namespace Bookify.Services.Booking.Infrastructure.Notifications.Smtp;

internal sealed class SmtpBookingNotificationSender : IBookingNotificationSender
{
    private readonly SmtpNotificationOptions _options;
    private readonly ILogger<SmtpBookingNotificationSender> _logger;

    public SmtpBookingNotificationSender(
        IOptions<SmtpNotificationOptions> options,
        ILogger<SmtpBookingNotificationSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;

        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result> SendAsync(BookingNotificationMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        Result validationResult = ValidateMessage(message);

        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        if (!_options.Enabled)
        {
            return Result.Failure(BookingNotificationErrors.DeliveryDisabled);
        }

        if (!MailboxAddress.TryParse(message.RecipientEmail.Trim(), out MailboxAddress? recipientAddress) ||
            recipientAddress is null)
        {
            return Result.Failure(BookingNotificationErrors.InvalidRecipient);
        }

        var email = new MimeMessage();

        email.From.Add(new MailboxAddress(_options.FromName.Trim(), _options.FromAddress.Trim()));
        email.To.Add(recipientAddress);
        email.Subject = message.Subject.Trim();

        email.Body = new TextPart("plain")
        {
            Text = message.Body
        };

        using var smtpClient = new SmtpClient
        {
            Timeout = _options.TimeoutMilliseconds
        };

        try
        {
            await smtpClient.ConnectAsync(
                _options.Host.Trim(),
                _options.Port,
                _options.Security,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await smtpClient.AuthenticateAsync(
                    _options.Username.Trim(),
                    _options.Password,
                    cancellationToken);
            }

            await smtpClient.SendAsync(email, cancellationToken);

            _logger.LogInformation(
                "Booking notification accepted by SMTP server for booking {BookingId}.", message.BookingId);

            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            return HandleDeliveryFailure(message, exception);
        }
        catch (Exception exception) when (IsExpectedSmtpException(exception))
        {
            return HandleDeliveryFailure(message, exception);
        }
        finally
        {
            if (smtpClient.IsConnected)
            {
                try
                {
                    await smtpClient.DisconnectAsync(quit: true, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "SMTP disconnect failed after processing booking notification {BookingId}.",
                        message.BookingId);
                }
            }
        }
    }

    private Result HandleDeliveryFailure(
        BookingNotificationMessage message,
        Exception exception)
    {
        _logger.LogWarning(
            exception,
            "SMTP delivery failed for booking {BookingId}.",
            message.BookingId);

        return Result.Failure(BookingNotificationErrors.DeliveryFailed(message.BookingId));
    }

    private static Result ValidateMessage(BookingNotificationMessage message)
    {
        if (message.BookingId == Guid.Empty)
        {
            return Result.Failure(BookingNotificationErrors.InvalidBookingId);
        }

        if (string.IsNullOrWhiteSpace(message.RecipientEmail))
        {
            return Result.Failure(BookingNotificationErrors.InvalidRecipient);
        }

        if (string.IsNullOrWhiteSpace(message.Subject) ||
            message.Subject.Contains('\r', StringComparison.Ordinal) ||
            message.Subject.Contains('\n', StringComparison.Ordinal))
        {
            return Result.Failure(BookingNotificationErrors.InvalidSubject);
        }

        if (string.IsNullOrWhiteSpace(message.Body))
        {
            return Result.Failure(BookingNotificationErrors.InvalidBody);
        }

        return Result.Success();
    }

    private static bool IsExpectedSmtpException(Exception exception)
    {
        return exception is SmtpCommandException or
            SmtpProtocolException or
            MailKitAuthenticationException or
            MailKitSslHandshakeException or
            SocketException or
            IOException or
            TimeoutException or
            NotSupportedException;
    }
}
