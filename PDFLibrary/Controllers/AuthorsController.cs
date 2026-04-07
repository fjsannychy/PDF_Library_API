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
  
    public class AuthorsController : Controller
    {
        private readonly DapperContext _context;

       
        public AuthorsController(DapperContext context)
        {
            _context = context;
        }


        [HttpPost("search")]
        public async Task<IActionResult> GetAll([FromBody] AuthorFilter filter)
        {
            using var connection = _context.CreateConnection();
            var sql = @"SELECT *
                FROM Authors
                WHERE Name LIKE @SearchText
                ORDER BY Name
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY;";
            var parameters = new DynamicParameters();
            int offset = (filter.PageNumber-1) * filter.PageSize;
            parameters.Add("SearchText", $"%{filter.Search}%");
            parameters.Add("Offset", offset);
            parameters.Add("PageSize", filter.PageSize);
            var Authors = await connection.QueryAsync<Author>(sql, parameters);

            var countSql = "select count(*) from Authors WHERE Name LIKE @SearchText";
            var count = await connection.QueryFirstAsync<int>(countSql, parameters);

            var result = new AuthorViewModel()
            {
                Authors = Authors,
                TotalCount = count,
            };


            return Ok(result);


        }
        // GET BY ID: api/Author/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            using var connection = _context.CreateConnection();
            var sql = "SELECT * FROM Authors WHERE Id = @Id";
            var Author = await connection.QueryFirstOrDefaultAsync<Author>(sql, new { Id = id });

            if (Author == null) return NotFound(new { message = "Author not found" });
            return Ok(Author);
        }
        // CREATE Author: api/Author
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] Author Author)
        {
            if (string.IsNullOrEmpty(Author.Name)) return BadRequest("Name is required");

            using var connection = _context.CreateConnection();
            // Using MySQL syntax as per your HeadController example
            var sql = @"INSERT INTO Authors (Name, Address, Contact, Status) VALUES (@Name, @Address, @Contact, @Status);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = await connection.ExecuteScalarAsync<int>(sql, Author);
            Author.Id = id;

            return CreatedAtAction(nameof(GetById), new { id = Author.Id }, Author);
        }
        // UPDATE Author: api/Author/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Author Author)
        {
            using var connection = _context.CreateConnection();
            var sql = "UPDATE Authors SET Name = @Name, Address = @Address, Contact = @Contact,  Status = @Status WHERE Id = @Id";

            var affectedRows = await connection.ExecuteAsync(sql, new
            {
                Name = Author.Name,
                Address = Author.Address,
                Contact = Author.Contact,
                Status = Author.Status,
                Id = id
            });

            if (affectedRows == 0) return NotFound();
            return NoContent();
        }
        // DELETE Author: api/Author/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                using var connection = _context.CreateConnection();
                var sql = "DELETE FROM Authors WHERE Id = @Id";
                var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });

                if (affectedRows == 0) return NotFound();
                return Ok(new { message = "Author deleted successfully" });
            }
            catch (Exception e) {
                return Ok(new { message = "Delete failed!" });
            }

        }
    }
}
