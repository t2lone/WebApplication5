using System.ComponentModel.DataAnnotations;
using WebApplication5.Models;

namespace WebApplication5.Dtos
{
    public class RoomRequest
    {
        [Required(ErrorMessage = "Название комнаты не может быть пустым.")]
        public string Name { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Вместимость должна быть больше нуля.")]
        public int Capacity { get; set; }

        public bool HasProjector { get; set; }
        public bool HasWhiteboard { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class EmployeeRequest
    {
        [Required(ErrorMessage = "ФИО не может быть пустым.")]
        public string FullName { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email не может быть пустым.")]
        [EmailAddress(ErrorMessage = "Некорректный Email.")]
        public string Email { get; set; } = string.Empty;
    }

    public class UpdateBookingRequest
    {
        public int RoomId { get; set; }

        [Required(ErrorMessage = "Укажите время начала.")]
        public DateTimeOffset? StartTime { get; set; }

        [Required(ErrorMessage = "Укажите время окончания.")]
        public DateTimeOffset? EndTime { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Количество участников должно быть больше нуля.")]
        public int ParticipantsCount { get; set; }

        [Required(ErrorMessage = "Тема встречи не может быть пустой.")]
        public string Topic { get; set; } = string.Empty;
    }

    public class CreateBookingRequest : UpdateBookingRequest
    {
        public int EmployeeId { get; set; }
    }

    public class BookingStatusRequest
    {
        [Required(ErrorMessage = "Укажите статус бронирования.")]
        public string Status { get; set; } = string.Empty;
    }

    public record BookingResponse(int Id, int RoomId, string RoomName, int EmployeeId,
        string EmployeeFullName, DateTimeOffset StartTime, DateTimeOffset EndTime,
        int ParticipantsCount, string Topic, BookingStatus Status);
}