using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebApplication5.Data;
using WebApplication5.Dtos;
using WebApplication5.Models;

namespace WebApplication5.Controllers
{
    [ApiController]
    [Route("api/bookings")]
    public class BookingsController(BookingStore store) : ControllerBase
    {
        [HttpGet]
        public IActionResult GetAll([FromQuery] int? employeeId, [FromQuery] int? roomId,
            [FromQuery] DateOnly? date, [FromQuery] string? status)
        {
            BookingStatus? filterStatus = null;
            if (status != null)
            {
                if (!BookingStore.TryParseStatus(status, out var parsed))
                    return BadRequest(new { message = "Неизвестный статус бронирования." });
                filterStatus = parsed;
            }

            lock (store.SyncRoot)
            {
                var bookings = store.Bookings.Values.Where(booking =>
                    (!employeeId.HasValue || booking.EmployeeId == employeeId)
                    && (!roomId.HasValue || booking.RoomId == roomId)
                    && (!date.HasValue || DateOnly.FromDateTime(booking.StartTime.UtcDateTime) == date)
                    && (!filterStatus.HasValue || booking.Status == filterStatus))
                    .OrderBy(booking => booking.Id).Select(store.ToResponse).ToArray();
                return Ok(bookings);
            }
        }

        [HttpGet("{id:int}")]
        public IActionResult GetById(int id)
        {
            lock (store.SyncRoot)
                return store.Bookings.TryGetValue(id, out var booking)
                    ? Ok(store.ToResponse(booking))
                    : NotFound(new { message = "Бронирование не найдено." });
        }

        [HttpGet("{id:int}/history")]
        public IActionResult GetHistory(int id)
        {
            lock (store.SyncRoot)
            {
                var logs = store.Logs.Where(log => log.BookingId == id).OrderBy(log => log.Id).ToArray();
                if (!store.Bookings.ContainsKey(id) && logs.Length == 0)
                    return NotFound(new { message = "Бронирование не найдено." });
                return Ok(logs);
            }
        }

        [HttpPost]
        public IActionResult Create(CreateBookingRequest request)
        {
            lock (store.SyncRoot)
            {
                var error = ValidateBooking(request, request.EmployeeId);
                if (error != null)
                    return error;

                var booking = new Booking(store.NextBookingId(), request.RoomId, request.EmployeeId,
                    request.StartTime!.Value.ToUniversalTime(), request.EndTime!.Value.ToUniversalTime(),
                    request.ParticipantsCount, request.Topic.Trim(), BookingStatus.Planned);
                store.Bookings.Add(booking.Id, booking);
                store.AddLog(booking.Id, "Created", JsonSerializer.Serialize(store.ToResponse(booking)));
                return CreatedAtAction(nameof(GetById), new { id = booking.Id }, store.ToResponse(booking));
            }
        }

        [HttpPut("{id:int}")]
        public IActionResult Update(int id, UpdateBookingRequest request)
        {
            lock (store.SyncRoot)
            {
                if (!store.Bookings.TryGetValue(id, out var booking))
                    return NotFound(new { message = "Бронирование не найдено." });
                if (booking.Status is BookingStatus.Finished or BookingStatus.Cancelled)
                    return Conflict(new { message = "Завершённое или отменённое бронирование нельзя изменить." });

                var error = ValidateBooking(request, booking.EmployeeId, id);
                if (error != null)
                    return error;

                var updated = booking with
                {
                    RoomId = request.RoomId,
                    StartTime = request.StartTime!.Value.ToUniversalTime(),
                    EndTime = request.EndTime!.Value.ToUniversalTime(),
                    ParticipantsCount = request.ParticipantsCount,
                    Topic = request.Topic.Trim()
                };
                store.Bookings[id] = updated;
                store.AddLog(id, "Updated", JsonSerializer.Serialize(new { before = booking, after = updated }));
                return Ok(store.ToResponse(updated));
            }
        }

        [HttpPut("{id:int}/status")]
        public IActionResult ChangeStatus(int id, BookingStatusRequest request)
        {
            if (!BookingStore.TryParseStatus(request.Status, out var status))
                return BadRequest(new { message = "Неизвестный статус бронирования." });

            lock (store.SyncRoot)
            {
                if (!store.Bookings.TryGetValue(id, out var booking))
                    return NotFound(new { message = "Бронирование не найдено." });

                var allowed = booking.Status switch
                {
                    BookingStatus.Planned => status is BookingStatus.Started or BookingStatus.Cancelled,
                    BookingStatus.Started => status == BookingStatus.Finished,
                    _ => false
                };
                if (!allowed)
                    return Conflict(new { message = $"Переход {booking.Status} -> {status} запрещён." });

                store.Bookings[id] = booking with { Status = status };
                store.AddLog(id, status == BookingStatus.Cancelled ? "Cancelled" : "StatusChanged",
                    $"{booking.Status} -> {status}");
                return Ok(store.ToResponse(store.Bookings[id]));
            }
        }

        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            lock (store.SyncRoot)
            {
                if (!store.Bookings.TryGetValue(id, out var booking))
                    return NotFound(new { message = "Бронирование не найдено." });
                if (booking.Status is BookingStatus.Started or BookingStatus.Finished)
                    return Conflict(new { message = "Начатое или завершённое бронирование нельзя удалить." });

                store.AddLog(id, "Deleted", JsonSerializer.Serialize(store.ToResponse(booking)));
                store.Bookings.Remove(id);
                return NoContent();
            }
        }

        private IActionResult? ValidateBooking(UpdateBookingRequest request, int employeeId, int? exceptId = null)
        {
            if (!store.Rooms.TryGetValue(request.RoomId, out var room))
                return NotFound(new { message = "Комната не найдена." });
            if (!store.Employees.ContainsKey(employeeId))
                return NotFound(new { message = "Сотрудник не найден." });
            if (!request.StartTime.HasValue || !request.EndTime.HasValue || request.StartTime >= request.EndTime)
                return BadRequest(new { message = "Время начала должно быть меньше времени окончания." });
            if (request.StartTime < DateTimeOffset.UtcNow)
                return BadRequest(new { message = "Время начала не может находиться в прошлом." });
            if (!room.IsActive)
                return Conflict(new { message = "Комната неактивна." });
            if (request.ParticipantsCount > room.Capacity)
                return Conflict(new { message = "Вместимость комнаты недостаточна." });
            if (store.HasOverlap(room.Id, request.StartTime.Value, request.EndTime.Value, exceptId))
                return Conflict(new { message = "Комната уже забронирована на выбранный интервал." });
            return null;
        }
    }
}