namespace Bookings.Domain.Models;

/// <summary>Статуси бронювання (CHECK CK_Bookings_Status у Лаб. 01).</summary>
public static class BookingStatus
{
    public const string Created = "Created";
    public const string AwaitingPayment = "AwaitingPayment";
    public const string Paid = "Paid";
    public const string CheckedIn = "CheckedIn";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string> { Created, AwaitingPayment, Paid, CheckedIn, Cancelled };
}

public static class TicketTypes
{
    public const string Adult = "Adult";
    public const string Student = "Student";
    public const string Child = "Child";

    /// <summary>Коефіцієнт ціни (такий самий, як у usp_Booking_AddTicket).</summary>
    public static decimal PriceFactor(string type) => type switch
    {
        Student => 0.8m,
        Child => 0.6m,
        _ => 1m
    };
}
