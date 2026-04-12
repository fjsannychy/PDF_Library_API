using Blog.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using PDFLibrary.Models.DTOs;
using PDFLibrary.Models.Entities;

namespace PDFLibrary.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UserFavouriteController : Controller
    {
        private readonly DapperContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserFavouriteController(DapperContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> ToggleFavourite([FromBody] FavouriteDto dto)
        {
            // Get logged-in user's ID
            var userId = int.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("Id").Value);

            using var connection = _context.CreateConnection();

            // 1. Check if the record already exists
            string checkSql = "SELECT Id FROM UserFavourites WHERE UserId = @UserId AND BookId = @BookId";
            var existingId = await connection.QueryFirstOrDefaultAsync<int?>(checkSql, new { UserId = userId, BookId = dto.BookId });

            if (existingId.HasValue)
            {
                // DOUBLE CLICK / UNDO: Record exists, so we delete it
                string deleteSql = "DELETE FROM UserFavourites WHERE Id = @Id";
                await connection.ExecuteAsync(deleteSql, new { Id = existingId.Value });
                return Ok(new { status = "removed", message = "Book removed from favourites." });
            }
            else
            {
                // MARK AS FAVOURITE: Record doesn't exist, so we insert it
                string insertSql = "INSERT INTO UserFavourites (UserId, BookId) VALUES (@UserId, @BookId)";
                await connection.ExecuteAsync(insertSql, new { UserId = userId, BookId = dto.BookId });
                return Ok(new { status = "added", message = "Book added to favourites." });
            }
        }

        // Get the list of IDs for the user's first tab
        [HttpGet("my-favourites")]
        public async Task<IActionResult> GetMyFavourites()
        {
            var userId = int.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("Id").Value);

            using var connection = _context.CreateConnection();
            string sql = "SELECT BookId FROM UserFavourites WHERE UserId = @UserId";

            var favouriteBookIds = await connection.QueryAsync<int>(sql, new { UserId = userId });
            return Ok(favouriteBookIds);
        }
    }
}