using System.Text.Json;
using WebApplication5.Dtos;
using WebApplication5.Models;

namespace WebApplication5.Data
{
    public class BookingStore
    {
        public object SyncRoot { get; } = new();
        public Dictionary<int, Room> Rooms { get; } = new();
        public Dictionary<int, Employee> Employees { get; } = new();
        public Dictionary<int, Booking> Bookings { get; } = new();
        public List<BookingLog> Logs { get; } = new();

        private int roomId;
        private int employeeId;
        private int bookingId;
        private int logId;

        public int NextRoomId() => ++roomId;
        public int NextEmployeeId() => ++employeeId;
        public int NextBookingId() => ++bookingId;

        public BookingStore()
        {
            Room[] rooms =
            [
                new(NextRoomId(), "Орион", 4, false, true, true),
                new(NextRoomId(), "Сириус", 8, true, true, true),
                new(NextRoomId(), "Вега", 12, true, false, true),
                new(NextRoomId(), "Альтаир", 20, true, true, true),
                new(NextRoomId(), "Лира", 6, false, false, true),
                new(NextRoomId(), "Феникс", 10, true, true, false)
            ];
            foreach (var room in rooms)
                Rooms.Add(room.Id, room);

            Employee[] employees =
            [
                new(NextEmployeeId(), "Иван Петров", "Разработка", "ivan.petrov@company.ru"),
                new(NextEmployeeId(), "Анна Иванова", "Маркетинг", "anna.ivanova@company.ru"),
                new(NextEmployeeId(), "Сергей Смирнов", "Продажи", "sergey.smirnov@company.ru"),
                new(NextEmployeeId(), "Мария Соколова", "HR", "maria.sokolova@company.ru"),
                new(NextEmployeeId(), "Дмитрий Волков", "Разработка", "dmitry.volkov@company.ru"),
                new(NextEmployeeId(), "Елена Кузнецова", "Финансы", "elena.kuznetsova@company.ru"),
                new(NextEmployeeId(), "Алексей Морозов", "Поддержка", "alexey.morozov@company.ru"),
                new(NextEmployeeId(), "Ольга Новикова", "Юридический отдел", "olga.novikova@company.ru")
            ];
            foreach (var employee in employees)
                Employees.Add(employee.Id, employee);

            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < 10; i++)
            {
                var status = i switch
                {
                    0 => BookingStatus.Started,
                    1 or 2 => BookingStatus.Finished,
                    3 or 4 => BookingStatus.Cancelled,
                    _ => BookingStatus.Planned
                };
                var start = status switch
                {
                    BookingStatus.Started => now.AddMinutes(-15),
                    BookingStatus.Finished => now.AddDays(-i).AddHours(-2),
                    _ => new DateTimeOffset(now.UtcDateTime.Date.AddDays(i + 1).AddHours(9 + i % 3), TimeSpan.Zero)
                };
                var room = Rooms[i % 5 + 1];
                var booking = new Booking(NextBookingId(), room.Id, i % 8 + 1, start,
                    start.AddHours(1), Math.Min(room.Capacity, 3 + i), $"Рабочая встреча {i + 1}", status);
                Bookings.Add(booking.Id, booking);
                AddLog(booking.Id, "Created", JsonSerializer.Serialize(booking with { Status = BookingStatus.Planned }));
                if (status is BookingStatus.Started or BookingStatus.Finished)
                    AddLog(booking.Id, "StatusChanged", "Planned -> Started");
                if (status == BookingStatus.Finished)
                    AddLog(booking.Id, "StatusChanged", "Started -> Finished");
                if (status == BookingStatus.Cancelled)
                    AddLog(booking.Id, "Cancelled", "Planned -> Cancelled");
            }
        }

        public void AddLog(int id, string action, string details)
        {
            Logs.Add(new BookingLog(++logId, id, DateTimeOffset.UtcNow, action, details));
        }

        public bool HasOverlap(int room, DateTimeOffset start, DateTimeOffset end, int? exceptId = null)
        {
            return Bookings.Values.Any(booking => booking.Id != exceptId && booking.RoomId == room
                && booking.Status is BookingStatus.Planned or BookingStatus.Started
                && start < booking.EndTime && end > booking.StartTime);
        }

        public BookingResponse ToResponse(Booking booking)
        {
            return new BookingResponse(booking.Id, booking.RoomId, Rooms[booking.RoomId].Name,
                booking.EmployeeId, Employees[booking.EmployeeId].FullName, booking.StartTime,
                booking.EndTime, booking.ParticipantsCount, booking.Topic, booking.Status);
        }

        public static bool TryParseStatus(string value, out BookingStatus status)
        {
            status = default;
            return Enum.GetNames<BookingStatus>().Any(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
                && Enum.TryParse(value, true, out status);
        }
    }
}