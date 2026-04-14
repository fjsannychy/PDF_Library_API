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
            var userId = int.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("Id").Value);


            using var connection = _context.CreateConnection();

            var sql = $@"declare @UserMode int = (select 
                                              case when role = 'General' then 0 else 1 end 
                                             from users where Id = @UserId
                                             );

                    WITH UserPoints AS (
                        SELECT 
                            BookId,
                            SUM(Points) as TotalPoints
                        FROM (
                            SELECT BookId, 5 as Points FROM UserFavourites WHERE UserId = @UserId
                            UNION ALL
                            SELECT BookId, 4 FROM UserPayments WHERE UserId = @UserId
                            UNION ALL
                            SELECT BookId, 3 FROM UserActions WHERE UserId = @UserId AND ActionType = 2
                            UNION ALL
                            SELECT BookId, 2 FROM UserActions WHERE UserId = @UserId AND ActionType = 1
                            UNION ALL
                            SELECT BookId, 1 FROM UserActions WHERE UserId = @UserId AND ActionType = 0
                        ) t
                        GROUP BY BookId
                    ),
                    UserBooks as (
                            SELECT distinct BookId FROM UserFavourites WHERE UserId = @UserId
                            {(filter.FilterType == FilterType.Self ?
                            @"UNION ALL
                            SELECT distinct BookId FROM UserPayments WHERE UserId = @UserId" :
                            "")}
                        ),
                    CategoryScores AS (
                        SELECT b.CategoryId, SUM(up.TotalPoints) as CatPoints 
                        FROM UserPoints up JOIN Books b ON up.BookId = b.Id GROUP BY b.CategoryId
                    ),
                    AuthorScores AS (
                        SELECT b.AuthorId, SUM(up.TotalPoints) as AuthPoints 
                        FROM UserPoints up JOIN Books b ON up.BookId = b.Id GROUP BY b.AuthorId
                    ),
                    PublisherScores AS (
                        SELECT b.PublisherId, SUM(up.TotalPoints) as PubPoints 
                        FROM UserPoints up JOIN Books b ON up.BookId = b.Id GROUP BY b.PublisherId
                    )

                    SELECT 
                        b.[Id], b.[Title], b.[Price], b.[DiscountPercent], b.[PriceBeforeDiscount],
                        b.[CategoryId], b.[PublisherId], b.[AuthorId], b.[Edition], b.[Volume],
                        b.[ShortDescription], b.[Details], b.[CoverPhotoUrl], b.[PdfUrl],
                        b.[PublishDate], b.[RegisterDate], b.[RegisteredBy], b.[Status],
                        p.Name AS Publisher,
                        a.Name AS Author,
                        c.Name AS Category,
                        u.FullName AS RegisteredUser,
                        (SELECT STRING_AGG(bf.Title, ', ') FROM BookFeatures bf WHERE bf.BookId = b.Id) AS Features
                    FROM Books b
                    INNER JOIN Publishers p ON p.Id = b.PublisherId
                    INNER JOIN Authors a ON a.Id = b.AuthorId
                    INNER JOIN Categories c ON c.Id = b.CategoryId
                    INNER JOIN Users u ON u.Id = b.RegisteredBy
                    LEFT JOIN UserPoints up ON up.BookId = b.Id
                    LEFT JOIN AuthorScores ascr ON ascr.AuthorId = b.AuthorId
                    LEFT JOIN CategoryScores cscr ON cscr.CategoryId = b.CategoryId
                    LEFT JOIN PublisherScores pscr ON pscr.PublisherId = b.PublisherId
                    {(filter.FilterType != FilterType.Recommended ?
                     " Inner Join UserBooks ub on ub.BookId = b.Id " :
                     "")}
                    WHERE 
                        (@UserMode = 1 OR b.Status = 0)
                        AND (
                            b.Title LIKE @SearchText OR 
                            a.Name LIKE @SearchText OR 
                            c.Name LIKE @SearchText OR 
                            p.Name LIKE @SearchText
                        )
                    ORDER BY 
                        (ISNULL(up.TotalPoints, 0) + ISNULL(ascr.AuthPoints, 0) + 
                         ISNULL(cscr.CatPoints, 0) + ISNULL(pscr.PubPoints, 0)) DESC,
                        b.Title
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            var parameters = new DynamicParameters();
            int offset = (filter.PageNumber - 1) * filter.PageSize;
            parameters.Add("UserId", userId);
            parameters.Add("SearchText", $"%{filter.Search}%");
            parameters.Add("Offset", offset);
            parameters.Add("PageSize", filter.PageSize);
            var Books = await connection.QueryAsync<BookListItemViewModel>(sql, parameters);

            var countSql = $@"With 
                            UserBooks as (
                                    SELECT distinct BookId FROM UserFavourites WHERE UserId = @UserId
                                    {(filter.FilterType == FilterType.Self ?
                                    @"UNION ALL
                                    SELECT distinct BookId FROM UserPayments WHERE UserId = @UserId" :
                                    "")}
                                )
                            select count(*) 
                            from Books b
                            {(filter.FilterType != FilterType.Recommended ?
                             " Inner Join UserBooks ub on ub.BookId = b.Id " :
                             "")}
                            WHERE b.Title LIKE @SearchText";
            var count = await connection.QueryFirstAsync<int>(countSql, parameters);

            var result = new BookListViewModel()
            {
                Items = Books.ToList(),
                TotalCount = count,
            };


            return Ok(result);


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
        [HttpPost("process-payment")]
        public async Task<IActionResult> ProcessPayment([FromBody] int bookId)
        {
            var userId = int.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("Id").Value);
            using var connection = _context.CreateConnection();

            // 1. Get book price for the 'Amount' column
            var book = await connection.QueryFirstOrDefaultAsync<BookListItemViewModel>("SELECT * FROM Books WHERE Id = @Id", new { Id = bookId });
            if (book == null) return NotFound();

            // 2. Insert into UserPayments (Matching your DB schema image)
            var paySql = @"INSERT INTO UserPayments 
                   (BookId, UserId, Amount, PaymentAccount, PaymentType, PaymentRef, PaymentTime, Remarks) 
                   VALUES 
                   (@BookId, @UserId, @Amount, @Account, @Type, @Ref, GETUTCDATE(), @Remarks)";

            await connection.ExecuteAsync(paySql, new
            {
                BookId = bookId,
                UserId = userId,
                Amount = book.Price,
                Account = "Demo-Account-123", // Fake data
                Type = 1,                     // e.g., 1 for Card
                Ref = "REF-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                Remarks = "Fake purchase for testing"
            });

            // 3. Keep UserActions updated (ActionType 2 = Paid) for your recommendation points
            await connection.ExecuteAsync(@"INSERT INTO UserActions (UserId, BookId, ActionType, ActionTime) 
                                   VALUES (@UserId, @BookId, 2, GETUTCDATE())",
                                           new { UserId = userId, BookId = bookId });

            return Ok(new { message = "Payment successful!" });
        }
    }
}