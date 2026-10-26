using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WholesaleRetailStore.Data;
using WholesaleRetailStore.Models;
using WholesaleRetailStore.Models.DTOs;

namespace WholesaleRetailStore.Controllers
{
    /// <summary>
    /// REST API for customers.
    /// </summary>
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(AppDbContext context, ILogger<CustomerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// GET: api/customers
        /// Returns a list of all customers
        /// </summary>
        [HttpGet("api/customers")]
        public async Task<IActionResult> GetCustomers()
        {
            var customers = await _context.Customers
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => ToDto(c))
                .ToListAsync();

            return Ok(customers);
        }

        /// <summary>
        /// GET: api/customers/{id}
        /// Returns a specific customer by ID
        /// </summary>
        [HttpGet("api/customers/{id:int}")]
        public async Task<IActionResult> GetCustomer(int id)
        {
            var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (customer == null)
            {
                return NotFound(new { message = "Customer not found" });
            }

            return Ok(ToDto(customer));
        }

        /// <summary>
        /// POST: api/customers
        /// Creates a new customer. Emails are unique, compared case-insensitively.
        /// </summary>
        [HttpPost("api/customers")]
        public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerDto customerDto)
        {
            var email = customerDto.Email.Trim();
            if (await _context.Customers.AnyAsync(c => c.Email == email))
            {
                return Conflict(new { message = "A customer with this email already exists" });
            }

            var customer = new Customer
            {
                Name = customerDto.Name.Trim(),
                Email = email,
                CustomerType = customerDto.CustomerType
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created customer {CustomerId} ({CustomerType})", customer.Id, customer.CustomerType);

            return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, ToDto(customer));
        }

        /// <summary>
        /// PUT: api/customers/{id}
        /// Updates an existing customer
        /// </summary>
        [HttpPut("api/customers/{id:int}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] CreateCustomerDto customerDto)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound(new { message = "Customer not found" });
            }

            var email = customerDto.Email.Trim();
            if (await _context.Customers.AnyAsync(c => c.Email == email && c.Id != id))
            {
                return Conflict(new { message = "A customer with this email already exists" });
            }

            customer.Name = customerDto.Name.Trim();
            customer.Email = email;
            customer.CustomerType = customerDto.CustomerType;

            await _context.SaveChangesAsync();
            return Ok(ToDto(customer));
        }

        /// <summary>
        /// DELETE: api/customers/{id}
        /// Deletes a customer (only if they have no orders)
        /// </summary>
        [HttpDelete("api/customers/{id:int}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound(new { message = "Customer not found" });
            }

            if (await _context.Orders.AnyAsync(o => o.CustomerId == id))
            {
                return BadRequest(new { message = "Cannot delete customer with existing orders" });
            }

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static CustomerDto ToDto(Customer c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Email = c.Email,
            CustomerType = c.CustomerType
        };
    }
}
