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
  
    public class UserMarksController : Controller
    {
        private readonly DapperContext _context;
        private IHttpContextAccessor _httpContextAccessor;


        public UserMarksController(DapperContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }


        [HttpGet("get-all/{bookId}")]
        public async Task<IActionResult> GetAll(int? bookId)
        {

            var userId = int.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("Id").Value);

            using var connection = _context.CreateConnection();
            var sql = @"SELECT *
                        FROM UserMarks 
                        where BookId = @BookId and 
                              UserId = @UserId ";
            var parameters = new DynamicParameters();
            parameters.Add("BookId", bookId);
            parameters.Add("UserId", userId);
            var userMarks = await connection.QueryAsync<UserMark>(sql, parameters);

            return Ok(userMarks);


        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UserMark UserMark)
        {
            var userId = int.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("Id").Value);
            UserMark.UserId = userId;
            UserMark.MarkingTime = DateTime.UtcNow; 

            using var connection = _context.CreateConnection();
            // Using MySQL syntax as per your HeadController example
            var sql = @"INSERT INTO [PDFLibrary].[dbo].[UserMarks]
                                    (
                                        [BookId],
                                        [UserId],
                                        [PageNumber],
                                        [PositionTopX],
                                        [PositionTopY],
                                        [PositionBottomX],
                                        [PositionBottomY],
                                        [MarkingTime]
                                    )
                                    VALUES
                                    (
                                        @BookId,
                                        @UserId,
                                        @PageNumber,
                                        @PositionTopX,
                                        @PositionTopY,
                                        @PositionBottomX,
                                        @PositionBottomY,
                                        @MarkingTime
                                    ); SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = await connection.ExecuteScalarAsync<int>(sql, UserMark);
            UserMark.Id = id;

            return Ok(UserMark.Id);
        }

        // DELETE UserMark: api/UserMark/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = int.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("Id").Value);
            using var connection = _context.CreateConnection();
            var sql = "DELETE FROM UserMarks WHERE Id = @Id and UserId = @UserId";
            var affectedRows = await connection.ExecuteAsync(sql, new { Id = id, UserId = userId });

            if (affectedRows == 0) return NotFound();
            return Ok(new { message = "UserMark deleted successfully" });
        }
    }
}
