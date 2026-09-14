namespace CinemaReservation.Api.Services;

public class ReservationConflictException(
    string message,
    Exception? innerException = null)
    : Exception(message, innerException);