using Blog.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PDFLibrary.Models.DTOs;
using PDFLibrary.Models.Entities;
using PDFLibrary.Models.Enums;
using PDFLibrary.Models.ViewModels;

namespace PDFLibrary.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BooksController : Controller
    {
        private readonly DapperContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BooksController(DapperContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // ---------------------- SEARCH & RECOMMENDATIONS ----------------------
        [HttpPost("search")]
        public async Task<IActionResult> GetAll([FromBody] BookFilter filter)
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("Id")?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            int userId = int.Parse(userIdClaim);

            using var connection = _context.CreateConnection();
            var sql = $@"
                    declare @UserMode int = (select 
                                              case when role = 'General' then 0 else 1 end 
                                             from users where Id = @UserId
                                             );
                    With 
                    UserData as
                    (
                    select 
                    isnull(uf.BookId,isnull(up.BookId,isnull(uar.BookId,isnull(uao.BookId,uav.BookId)))) BookId,
                    (Sum(Case when uf.UserId is null then 0 else 5 end) + 
                     Sum(Case when  up.UserId is null then 0 else 4 end) + 
                     Sum(Case when  uar.UserId is null then 0 else 3 end) +
                     Sum(Case when  uao.UserId is null then 0 else 2 end) +
                     Sum(Case when  uav.UserId is null then 0 else 1 end)) Points 
                    FROM Users u 
                    left join UserActions uar on uar.UserId = u.Id and uar.ActionType = 2
                    left join UserActions uao on uao.UserId = u.Id and uao.ActionType = 1
                    left join UserActions uav on uav.UserId = u.Id and uav.ActionType = 0
                    left join UserFavourites uf on uf.UserId = u.Id  
                    left join UserPayments up on up.UserId = u.Id                
                    where u.Id = @UserId 
                    {(filter.FilterType == FilterType.Recommended ?
                      @"and (uf.BookId is not null or 
                            up.BookId is not null or
                            uar.BookId is not null or
                            uao.BookId is not null or
                            uav.BookId is not null)"
                      : filter.FilterType == FilterType.Favorites ?
                        @"and uf.BookId is not null"
                        : @"and (uf.BookId is not null or
                            up.BookId is not null )"
                     )}
                    group by isnull(uf.BookId,isnull(up.BookId,isnull(uar.BookId,isnull(uao.BookId,uav.BookId))))
                    )
                    SELECT 
                           b.[Id], b.[Title], b.[Price], b.[DiscountPercent], b.[PriceBeforeDiscount],
                           b.[CategoryId], b.[PublisherId], b.[AuthorId], b.[Edition], b.[Volume],
                           b.[ShortDescription], b.[Details], b.[CoverPhotoUrl], b.[PdfUrl],
                           b.[PublishDate], b.[RegisterDate], b.[RegisteredBy], b.[Status],
                           p.Name AS Publisher, a.Name AS Author, c.Name AS Category, u.FullName AS RegisteredUser,
                           STRING_AGG(bf.Title, ', ') AS Features
                    FROM Books b
                    Left JOIN BookFeatures bf ON bf.BookId = b.Id
                    Left JOIN BookFeatures bfb ON bfb.Title = bf.Title
                    INNER JOIN Publishers p ON p.Id = b.PublisherId
                    Left JOIN Books pb ON pb.PublisherId = p.Id
                    INNER JOIN Authors a ON a.Id = b.AuthorId
                    Left JOIN Books ab ON ab.AuthorId = a.Id
                    INNER JOIN Categories c ON c.Id = b.CategoryId
                    Left JOIN Books cb ON cb.CategoryId = c.Id
                    INNER JOIN Users u ON u.Id = b.RegisteredBy
                    {(filter.FilterType == FilterType.Recommended ? "Left" : "Inner")} JOIN UserData udb ON udb.BookId = b.Id
                    Left JOIN UserData udb_p ON udb_p.BookId = pb.Id
                    Left JOIN UserData udb_a ON udb_a.BookId = ab.Id
                    Left JOIN UserData udb_c ON udb_c.BookId = cb.Id
                    Left JOIN UserData udb_f ON udb_f.BookId = bfb.BookId
                    WHERE 
                    ((@UserMode = 0 and b.status = 0) or @UserMode = 1 ) and 
                    (b.Title LIKE @SearchText or bf.Title LIKE @SearchText or a.Name LIKE @SearchText or c.Name LIKE @SearchText or p.Name LIKE @SearchText)
                    GROUP BY 
                        b.Id, b.Title, b.Price, b.DiscountPercent, b.PriceBeforeDiscount,
                        b.CategoryId, b.PublisherId, b.AuthorId, b.Edition, b.Volume,
                        b.ShortDescription, b.Details, b.CoverPhotoUrl, b.PdfUrl,
                        b.PublishDate, b.RegisterDate, b.RegisteredBy, b.Status,
                        p.Name, a.Name, c.Name, u.Id, u.FullName
                    ORDER BY 
                    (Sum(isnull(udb.Points,0)) + Sum(isnull(udb_f.Points,0)) + Sum(isnull(udb_a.Points,0)) + Sum(isnull(udb_c.Points,0)) + Sum(isnull(udb_p.Points,0))) desc,
                    b.Title
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY;";

            var parameters = new DynamicParameters();
            int offset = (filter.PageNumber - 1) * filter.PageSize;
            parameters.Add("UserId", userId);
            parameters.Add("SearchText", $"%{filter.Search}%");
            parameters.Add("Offset", offset);
            parameters.Add("PageSize", filter.PageSize);

            var Books = await connection.QueryAsync<BookListItemViewModel>(sql, parameters);

            var countSql = "select count(*) from Books WHERE Title LIKE @SearchText";
            var count = await connection.QueryFirstAsync<int>(countSql, parameters);

            return Ok(new BookListViewModel { Items = Books.ToList(), TotalCount = count });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id, bool readMode)
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("Id")?.Value;
            int? userId = !string.IsNullOrEmpty(userIdClaim) ? int.Parse(userIdClaim) : null;

            using var connection = _context.CreateConnection();

            // 1. Log the view action
            if (userId.HasValue)
            {
                await connection.ExecuteAsync(@"INSERT INTO UserActions (UserId, BookId, ActionType, ActionTime) 
                                       VALUES (@UserId, @BookId, 0, GETUTCDATE())",
                                               new { UserId = userId, BookId = id });
            }

            // 2. The SQL Query (Optimized to prevent Error 500)
            var sql = @"SELECT b.*, 
                p.Name AS Publisher, 
                a.Name AS Author, 
                c.Name AS Category, 
                u.FullName AS RegisteredUser,
                -- Subquery for Favourites
                (SELECT CASE WHEN EXISTS (SELECT 1 FROM UserFavourites WHERE BookId = b.Id AND UserId = @UserId) 
                    THEN 1 ELSE 0 END) AS IsFavourited,
                -- Subquery for Paid/Read status
                (SELECT CASE WHEN EXISTS (SELECT 1 FROM UserActions WHERE BookId = b.Id AND UserId = @UserId AND ActionType = 2) 
                    THEN 1 ELSE 0 END) AS IsPaid
                FROM Books b
                INNER JOIN Publishers p ON p.Id = b.PublisherId
                INNER JOIN Authors a ON a.Id = b.AuthorId
                INNER JOIN Categories c ON c.Id = b.CategoryId
                INNER JOIN Users u ON u.Id = b.RegisteredBy 
                WHERE b.Id = @Id";

            try
            {
                var book = await connection.QueryFirstOrDefaultAsync<BookDetailsViewModel>(sql, new { Id = id, UserId = userId });

                if (book == null) return NotFound(new { message = "Book not found" });

                // 3. PDF URL Logic
                // access is true if price is 0 OR they have the 'Paid' action (IsPaid)
                bool hasAccess = book.Price == 0 || book.IsPaid;
                book.PdfUrl = (readMode && hasAccess) ? book.PdfUrl : null;

                // 4. Load Lists
                book.BookAttachments = (await connection.QueryAsync<BookAttachment>(@"SELECT * FROM BookAttachments WHERE BookId=@Id", new { Id = id })).ToList();
                book.Features = (await connection.QueryAsync<BookFeature>(@"SELECT * FROM BookFeatures WHERE BookId=@Id", new { Id = id })).Select(r => r.Title).ToList();

                return Ok(book);
            }
            catch (Exception ex)
            {
                // This helps you see the error in the "Response" tab in Chrome
                return StatusCode(500, new { message = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        // ---------------------- CREATE / UPDATE / DELETE (UPDATED) ----------------------

        [HttpPost]
        [Authorize(Roles = "Admin,Librarian")]
        public async Task<IActionResult> Create([FromForm] BookDTO book)
        {
            var userId = int.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("Id")!.Value);

            // UPDATE: Save Files and generate URL strings
            string coverUrl = await SaveFile(book.CoverPhoto, "covers");
            string pdfUrl = await SaveFile(book.PdfFile, "pdfs");

            using var connection = _context.CreateConnection();

            // UPDATE: Added CoverPhotoUrl and PdfUrl to INSERT
            var sql = @"INSERT INTO Books (Title, Price, DiscountPercent, PriceBeforeDiscount, CategoryId, PublisherId, AuthorId, Edition, Volume, ShortDescription, Details, Status, RegisteredBy, RegisterDate, CoverPhotoUrl, PdfUrl) 
                        VALUES (@Title, @Price, @DiscountPercent, @PriceBeforeDiscount, @CategoryId, @PublisherId, @AuthorId, @Edition, @Volume, @ShortDescription, @Details, @Status, @RegisteredBy, @RegisterDate, @CoverUrl, @PdfUrl);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var bookId = await connection.ExecuteScalarAsync<int>(sql, new
            {
                book.Title,
                book.Price,
                book.DiscountPercent,
                book.PriceBeforeDiscount,
                book.CategoryId,
                book.PublisherId,
                book.AuthorId,
                book.Edition,
                book.Volume,
                book.ShortDescription,
                book.Details,
                book.Status,
                RegisteredBy = userId,
                RegisterDate = DateTime.UtcNow,
                CoverUrl = coverUrl,
                PdfUrl = pdfUrl
            });

            // UPDATE: Handle Features
            if (book.Features != null && book.Features.Any())
            {
                foreach (var feature in book.Features)
                {
                    await connection.ExecuteAsync("INSERT INTO BookFeatures (BookId, Title) VALUES (@bookId, @feature)", new { bookId, feature });
                }
            }

            // UPDATE: Handle Extra Attachments
            if (book.bookAttachments != null && book.bookAttachments.Any())
            {
                foreach (var attachment in book.bookAttachments)
                {
                    string attUrl = await SaveFile(attachment.File, "attachments");
                    await connection.ExecuteAsync("INSERT INTO BookAttachments (BookId, FileUrl) VALUES (@bookId, @attUrl)", new { bookId, attUrl });
                }
            }

            return Ok(bookId);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Librarian")]
        public async Task<IActionResult> Update(int id, [FromForm] BookDTO book)
        {
            using var connection = _context.CreateConnection();

            // UPDATE: Handle new file uploads for existing record
            string coverUrl = book.CoverPhoto != null ? await SaveFile(book.CoverPhoto, "covers") : null;
            string pdfUrl = book.PdfFile != null ? await SaveFile(book.PdfFile, "pdfs") : null;

            // Build dynamic update string to avoid overwriting existing URLs with null if no new file is selected
            var sql = @"UPDATE Books SET Title=@Title, Price=@Price, DiscountPercent=@DiscountPercent, 
                        PriceBeforeDiscount=@PriceBeforeDiscount, CategoryId=@CategoryId, PublisherId=@PublisherId, 
                        AuthorId=@AuthorId, Edition=@Edition, Volume=@Volume, ShortDescription=@ShortDescription, 
                        Details=@Details, Status=@Status" +
                        (coverUrl != null ? ", CoverPhotoUrl=@CoverUrl" : "") +
                        (pdfUrl != null ? ", PdfUrl=@PdfUrl" : "") +
                        " WHERE Id=@Id";

            await connection.ExecuteAsync(sql, new
            {
                book.Title,
                book.Price,
                book.DiscountPercent,
                book.PriceBeforeDiscount,
                book.CategoryId,
                book.PublisherId,
                book.AuthorId,
                book.Edition,
                book.Volume,
                book.ShortDescription,
                book.Details,
                book.Status,
                CoverUrl = coverUrl,
                PdfUrl = pdfUrl,
                Id = id
            });

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Librarian")]
        public async Task<IActionResult> Delete(int id)
        {
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync("DELETE FROM BookAttachments WHERE BookId=@id", new { id });
            await connection.ExecuteAsync("DELETE FROM BookFeatures WHERE BookId=@id", new { id });
            await connection.ExecuteAsync("DELETE FROM UserActions WHERE BookId=@id", new { id });
            await connection.ExecuteAsync("DELETE FROM Books WHERE Id=@id", new { id });
            return Ok(new { message = "Book deleted successfully" });
        }

        [HttpPost("user-action")]
        public async Task<IActionResult> UserAction([FromBody] UserActionDTO request)
        {
            var userId = int.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("Id")!.Value);
            using var connection = _context.CreateConnection();

            var sql = @"INSERT INTO UserActions (UserId, BookId, ActionType, ActionTime) 
                        VALUES (@UserId, @BookId, @ActionType, @ActionTime);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId, BookId = request.BookId, ActionType = request.ActionType, ActionTime = DateTime.UtcNow });
            return Ok(id);
        }

        // UPDATE: Helper method to handle physical file storage
        private async Task<string> SaveFile(IFormFile? file, string subFolder)
        {
            if (file == null || file.Length == 0) return null;

            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", subFolder);
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/{subFolder}/{fileName}";
        }
        [HttpGet("purchased")]
        public async Task<IActionResult> GetPurchasedBooks()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("Id")?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            int userId = int.Parse(userIdClaim);

            using var connection = _context.CreateConnection();

            // This SQL joins Books with UserActions to find only purchased items
            var sql = @"SELECT b.*, p.Name AS Publisher, a.Name AS Author, c.Name AS Category
                FROM Books b
                INNER JOIN UserActions ua ON b.Id = ua.BookId
                INNER JOIN Publishers p ON p.Id = b.PublisherId
                INNER JOIN Authors a ON a.Id = b.AuthorId
                INNER JOIN Categories c ON c.Id = b.CategoryId
                WHERE ua.UserId = @UserId AND ua.ActionType = 2
                ORDER BY ua.ActionTime DESC";

            var books = await connection.QueryAsync<BookListItemViewModel>(sql, new { UserId = userId });
            return Ok(books);
        }
    }
}