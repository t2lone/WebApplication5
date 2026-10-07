using Microsoft.AspNetCore.Mvc;
using WebApplication5.Data;
using WebApplication5.Dtos;
using WebApplication5.Models;

namespace WebApplication5.Controllers
{
    [ApiController]
    [Route("api/employees")]
    public class EmployeesController(BookingStore store) : ControllerBase
    {
        [HttpGet]
        public IActionResult GetAll()
        {
            lock (store.SyncRoot)
                return Ok(store.Employees.Values.OrderBy(employee => employee.Id).ToArray());
        }

        [HttpGet("{id:int}")]
        public IActionResult GetById(int id)
        {
            lock (store.SyncRoot)
                return store.Employees.TryGetValue(id, out var employee)
                    ? Ok(employee)
                    : NotFound(new { message = "Сотрудник не найден." });
        }

        [HttpPost]
        public IActionResult Create(EmployeeRequest request)
        {
            lock (store.SyncRoot)
            {
                if (EmailExists(request.Email))
                    return Conflict(new { message = "Сотрудник с таким Email уже существует." });

                var employee = new Employee(store.NextEmployeeId(), request.FullName.Trim(),
                    request.Department.Trim(), request.Email.Trim());
                store.Employees.Add(employee.Id, employee);
                return CreatedAtAction(nameof(GetById), new { id = employee.Id }, employee);
            }
        }

        [HttpPut("{id:int}")]
        public IActionResult Update(int id, EmployeeRequest request)
        {
            lock (store.SyncRoot)
            {
                if (!store.Employees.ContainsKey(id))
                    return NotFound(new { message = "Сотрудник не найден." });
                if (EmailExists(request.Email, id))
                    return Conflict(new { message = "Сотрудник с таким Email уже существует." });

                var employee = new Employee(id, request.FullName.Trim(), request.Department.Trim(), request.Email.Trim());
                store.Employees[id] = employee;
                return Ok(employee);
            }
        }

        private bool EmailExists(string email, int? exceptId = null)
        {
            return store.Employees.Values.Any(employee => employee.Id != exceptId
                && string.Equals(employee.Email, email.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}