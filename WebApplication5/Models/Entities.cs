namespace WebApplication5.Models
{
    public enum BookingStatus
    {
        Planned,
        Started,
        Finished,
        Cancelled
    }

    public record Room(int Id, string Name, int Capacity, bool HasProjector, bool HasWhiteboard, bool IsActive);

    public record Employee(int Id, string FullName, string Department, string Email);

    public record Booking(int Id, int RoomId, int EmployeeId, DateTimeOffset StartTime,
        DateTimeOffset EndTime, int ParticipantsCount, string Topic, BookingStatus Status);

    public record BookingLog(int Id, int BookingId, DateTimeOffset CreatedAt, string Action, string Details);
}