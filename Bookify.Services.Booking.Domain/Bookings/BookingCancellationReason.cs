using System;
using System.Collections.Generic;
using System.Text;

namespace Bookify.Services.Booking.Domain.Bookings;

public enum BookingCancellationReason
{
    RejectedByOwner = 1,
    PaymentExpired = 2
}
