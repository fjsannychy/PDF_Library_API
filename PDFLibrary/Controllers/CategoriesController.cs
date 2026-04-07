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
  
    public class CategoriesController : Controller
    {
        private readonly DapperContext _context;

       
        public CategoriesController(DapperContext context)
        {
            _context = context;
        }


        [HttpPost("search")]
        public async Task<IActionResult> GetAll([FromBody] CategoryFilter filter)
        {
            using var connection = _context.CreateConnection();
            var sql = @"SELECT *
                FROM Categories
                WHERE Name LIKE @SearchText
                ORDER BY Name
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY;";
            var parameters = new DynamicParameters();
            int offset = (filter.PageNumber-1) * filter.PageSize;
            parameters.Add("SearchText", $"%{filter.Search}%");
            parameters.Add("Offset", offset);
            parameters.Add("PageSize", filter.PageSize);
            var Category = await connection.QueryAsync<Category>(sql, parameters);

            var countSql = "select count(*) from Categories WHERE Name LIKE @SearchText";
            var count = await connection.QueryFirstAsync<int>(countSql, parameters);

            var result = new CategoryViewModel()
            {
                Categories = Category,
                TotalCount = count,
            };


            return Ok(result);


        }
        // GET BY ID: api/Category/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            using var connection = _context.CreateConnection();
            var sql = "SELECT * FROM Categories WHERE Id = @Id";
            var Category = await connection.QueryFirstOrDefaultAsync<Category>(sql, new { Id = id });

            if (Category == null) return NotFound(new { message = "Category not found" });
            return Ok(Category);
        }
        // CREATE Category: api/Category
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] Category Category)
        {
            if (string.IsNullOrEmpty(Category.Name)) return BadRequest("Name is required");

            using var connection = _context.CreateConnection();
            // Using MySQL syntax as per your HeadController example
            var sql = @"INSERT INTO Categories (Name, Status) VALUES (@Name, @Status);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = await connection.ExecuteScalarAsync<int>(sql, Category);
            Category.Id = id;

            return CreatedAtAction(nameof(GetById), new { id = Category.Id }, Category);
        }
        // UPDATE Category: api/Category/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Category Category)
        {
            using var connection = _context.CreateConnection();
            var sql = "UPDATE Categories SET Name = @Name,  Status = @Status WHERE Id = @Id";

            var affectedRows = await connection.ExecuteAsync(sql, new
            {
                Name = Category.Name,
                Status = Category.Status,
                Id = id
            });

            if (affectedRows == 0) return NotFound();
            return NoContent();
        }
        // DELETE Category: api/Category/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            using var connection = _context.CreateConnection();
            var sql = "DELETE FROM Categories WHERE Id = @Id";
            var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });

            if (affectedRows == 0) return NotFound();
            return Ok(new { message = "Category deleted successfully" });
        }
    }
}
