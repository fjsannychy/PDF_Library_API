using Blog.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PDFLibrary.Models.DTOs;
using PDFLibrary.Models.Entities;
using PDFLibrary.Models.ViewModels;
namespace PDFLibrary.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
  
    public class PublishersController : Controller
    {
        private readonly DapperContext _context;

       
        public PublishersController(DapperContext context)
        {
            _context = context;
        }


        [HttpPost("search")]
        public async Task<IActionResult> GetAll([FromBody] PublisherFilter filter)
        {
            using var connection = _context.CreateConnection();
            var sql = @"SELECT *
                FROM Publishers
                WHERE Name LIKE @SearchText
                ORDER BY Name
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY;";
            var parameters = new DynamicParameters();
            int offset = (filter.PageNumber-1) * filter.PageSize;
            parameters.Add("SearchText", $"%{filter.Search}%");
            parameters.Add("Offset", offset);
            parameters.Add("PageSize", filter.PageSize);
            var Publishers = await connection.QueryAsync<Publisher>(sql, parameters);

            var countSql = "select count(*) from Publishers WHERE Name LIKE @SearchText";
            var count = await connection.QueryFirstAsync<int>(countSql, parameters);

            var result = new PublisherViewModel()
            {
                Publishers = Publishers,
                TotalCount = count,
            };


            return Ok(result);


        }
        // GET BY ID: api/Publisher/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            using var connection = _context.CreateConnection();
            var sql = "SELECT * FROM Publishers WHERE Id = @Id";
            var Publisher = await connection.QueryFirstOrDefaultAsync<Publisher>(sql, new { Id = id });

            if (Publisher == null) return NotFound(new { message = "Publisher not found" });
            return Ok(Publisher);
        }
        // CREATE Publisher: api/Publisher
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] Publisher Publisher)
        {
            if (string.IsNullOrEmpty(Publisher.Name)) return BadRequest("Name is required");

            using var connection = _context.CreateConnection();
            // Using MySQL syntax as per your HeadController example
            var sql = @"INSERT INTO Publishers (Name, Address, Contact, Status) VALUES (@Name, @Address, @Contact, @Status);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = await connection.ExecuteScalarAsync<int>(sql, Publisher);
            Publisher.Id = id;

            return CreatedAtAction(nameof(GetById), new { id = Publisher.Id }, Publisher);
        }
        // UPDATE Publisher: api/Publisher/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Publisher Publisher)
        {
            using var connection = _context.CreateConnection();
            var sql = "UPDATE Publishers SET Name = @Name, Address = @Address, Contact = @Contact,  Status = @Status WHERE Id = @Id";

            var affectedRows = await connection.ExecuteAsync(sql, new
            {
                Name = Publisher.Name,
                Address = Publisher.Address,
                Contact = Publisher.Contact,
                Status = Publisher.Status,
                Id = id
            });

            if (affectedRows == 0) return NotFound();
            return NoContent();
        }
        // DELETE Publisher: api/Publisher/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            using var connection = _context.CreateConnection();
            var sql = "DELETE FROM Publishers WHERE Id = @Id";
            var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });

            if (affectedRows == 0) return NotFound();
            return Ok(new { message = "Publisher deleted successfully" });
        }
    }
}
