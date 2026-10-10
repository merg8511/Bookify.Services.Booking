using System;
using System.Collections.Generic;
using System.Text;

namespace Bookify.Services.Booking.Application.Notifications;

public enum BookingNotificationDispatchOutcome
{
    Sent = 1,
    Skipped = 2
}
