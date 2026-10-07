using Microsoft.AspNetCore.Mvc;
using WebApplication5.Data;
using WebApplication5.Dtos;
using WebApplication5.Models;

namespace WebApplication5.Controllers
{
    [ApiController]
    [Route("api/rooms")]
    public class RoomsController(BookingStore store) : ControllerBase
    {
        [HttpGet]
        public IActionResult GetAll()
        {
            lock (store.SyncRoot)
                return Ok(store.Rooms.Values.OrderBy(room => room.Id).ToArray());
        }

        [HttpGet("{id:int}")]
        public IActionResult GetById(int id)
        {
            lock (store.SyncRoot)
                return store.Rooms.TryGetValue(id, out var room)
                    ? Ok(room)
                    : NotFound(new { message = "Комната не найдена." });
        }

        [HttpGet("available")]
        public IActionResult GetAvailable([FromQuery] DateTimeOffset? start, [FromQuery] DateTimeOffset? end,
            [FromQuery] int? participants, [FromQuery] bool projector = false, [FromQuery] bool whiteboard = false)
        {
            if (start == null || end == null || start >= end || participants == null || participants <= 0)
                return BadRequest(new { message = "Укажите корректный интервал start < end и положительное participants." });

            lock (store.SyncRoot)
            {
                var rooms = store.Rooms.Values.Where(room => room.IsActive && room.Capacity >= participants
                    && (!projector || room.HasProjector) && (!whiteboard || room.HasWhiteboard)
                    && !store.HasOverlap(room.Id, start.Value, end.Value)).OrderBy(room => room.Id).ToArray();
                return Ok(rooms);
            }
        }

        [HttpPost]
        public IActionResult Create(RoomRequest request)
        {
            lock (store.SyncRoot)
            {
                if (NameExists(request.Name))
                    return Conflict(new { message = "Комната с таким названием уже существует." });

                var room = new Room(store.NextRoomId(), request.Name.Trim(), request.Capacity,
                    request.HasProjector, request.HasWhiteboard, request.IsActive);
                store.Rooms.Add(room.Id, room);
                return CreatedAtAction(nameof(GetById), new { id = room.Id }, room);
            }
        }

        [HttpPut("{id:int}")]
        public IActionResult Update(int id, RoomRequest request)
        {
            lock (store.SyncRoot)
            {
                if (!store.Rooms.ContainsKey(id))
                    return NotFound(new { message = "Комната не найдена." });
                if (NameExists(request.Name, id))
                    return Conflict(new { message = "Комната с таким названием уже существует." });

                var room = new Room(id, request.Name.Trim(), request.Capacity,
                    request.HasProjector, request.HasWhiteboard, request.IsActive);
                store.Rooms[id] = room;
                return Ok(room);
            }
        }

        [HttpPut("{id:int}/deactivate")]
        public IActionResult Deactivate(int id)
        {
            lock (store.SyncRoot)
            {
                if (!store.Rooms.TryGetValue(id, out var room))
                    return NotFound(new { message = "Комната не найдена." });
                if (!room.IsActive)
                    return Conflict(new { message = "Комната уже неактивна." });

                room = room with { IsActive = false };
                store.Rooms[id] = room;
                return Ok(room);
            }
        }

        private bool NameExists(string name, int? exceptId = null)
        {
            return store.Rooms.Values.Any(room => room.Id != exceptId
                && string.Equals(room.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}