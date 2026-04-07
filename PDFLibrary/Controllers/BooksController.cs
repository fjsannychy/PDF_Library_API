using Blog.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
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

       
        public BooksController(DapperContext context, 
                               IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }


        [HttpPost("search")]
        public async Task<IActionResult> GetAll([FromBody] BookFilter filter)
        {
            var userId = int.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("Id").Value);


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
                    left join UserActions uar on uar.UserId = u.Id and 
                                                uar.ActionType = 2
                    left join UserActions uao on uao.UserId = u.Id and 
                                                uao.ActionType = 1
                    left join UserActions uav on uav.UserId = u.Id and 
                                                uav.ActionType = 0
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
                     )
                     }
                    group by isnull(uf.BookId,isnull(up.BookId,isnull(uar.BookId,isnull(uao.BookId,uav.BookId))))
                    )
                    SELECT 
                           b.[Id]
                          ,b.[Title]
                          ,b.[Price]
                          ,b.[DiscountPercent]
                          ,b.[PriceBeforeDiscount]
                          ,b.[CategoryId]
                          ,b.[PublisherId]
                          ,b.[AuthorId]
                          ,b.[Edition]
                          ,b.[Volume]
                          ,b.[ShortDescription]
                          ,b.[Details]
                          ,b.[CoverPhotoUrl]
                          ,b.[PdfUrl]
                          ,b.[PublishDate]
                          ,b.[RegisterDate]
                          ,b.[RegisteredBy]
                          ,b.[Status]
                          ,p.Name AS Publisher
                          ,a.Name AS Author
                          ,c.Name AS Category
                          ,u.FullName AS RegisteredUser
                          ,STRING_AGG(bf.Title, ', ') AS Features
                    FROM Books b
                    Left JOIN BookFeatures bf ON bf.BookId = b.Id
                    Left JOIN BookFeatures bfb ON bfb.Title = bf.Title
                    INNER JOIN Publishers p ON p.Id = b.PublisherId
                    INNER JOIN Books pb ON pb.PublisherId = p.Id
                    INNER JOIN Authors a ON a.Id = b.AuthorId
                    INNER JOIN Books ab ON ab.AuthorId = a.Id
                    INNER JOIN Categories c ON c.Id = b.CategoryId
                    INNER JOIN Books cb ON cb.CategoryId = c.Id
                    INNER JOIN Users u ON u.Id = b.RegisteredBy
                    {(filter.FilterType == FilterType.Recommended ? 
                     "Left" :
                     "Inner")} JOIN UserData udb ON udb.BookId = b.Id
                    Left JOIN UserData udb_p ON udb_p.BookId = pb.Id
                    Left JOIN UserData udb_a ON udb_a.BookId = ab.Id
                    Left JOIN UserData udb_c ON udb_c.BookId = cb.Id
                    Left JOIN UserData udb_f ON udb_f.BookId = bfb.BookId
                    WHERE 
                    ((@UserMode = 0 and b.status = 0) or @UserMode = 1 ) and 
                    (b.Title LIKE @SearchText or
                     bf.Title LIKE @SearchText or
                     a.Name LIKE @SearchText or
                     c.Name LIKE @SearchText or
                     p.Name LIKE @SearchText
                    )
                    GROUP BY 
                        b.Id, b.Title, b.Price, b.DiscountPercent, b.PriceBeforeDiscount,
                        b.CategoryId, b.PublisherId, b.AuthorId, b.Edition, b.Volume,
                        b.ShortDescription, b.Details, b.CoverPhotoUrl, b.PdfUrl,
                        b.PublishDate, b.RegisterDate, b.RegisteredBy, b.Status,
                        p.Name, a.Name, c.Name, u.Id, u.FullName
                    ORDER BY 
                    (Sum(isnull(udb.Points,0)) +
                     Sum(isnull(udb_f.Points,0)) +
                     Sum(isnull(udb_a.Points,0)) +
                     Sum(isnull(udb_c.Points,0)) +
                     Sum(isnull(udb_p.Points,0))) desc,
                    b.Title
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY;";
            var parameters = new DynamicParameters();
            int offset = (filter.PageNumber-1) * filter.PageSize;
            parameters.Add("UserId", userId);
            parameters.Add("SearchText", $"%{filter.Search}%");
            parameters.Add("Offset", offset);
            parameters.Add("PageSize", filter.PageSize);
            var Books = await connection.QueryAsync<BookListItemViewModel>(sql, parameters);

            var countSql = "select count(*) from Books WHERE Title LIKE @SearchText";
            var count = await connection.QueryFirstAsync<int>(countSql, parameters);

            var result = new BookListViewModel()
            {
                Items = Books.ToList(),
                TotalCount = count,
            };


            return Ok(result);


        }


        // GET BY ID: api/Book/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id, bool readMode)
        {
            using var connection = _context.CreateConnection();
            var sql = $@"SELECT b.*, p.Name Publisher, a.Name Author, c.Name Category, u.FullName  RegisteredUser FROM 
                         Books b
                         inner join Publishers p on p.Id = b.PublisherId
                         inner join Authors a on a.Id = b.AuthorId
                         inner join Categories c on c.Id = b.CategoryId
                         inner join Users u on u.Id = b.RegisteredBy 
                         WHERE b.Id = @Id";
            var Book = await connection.QueryFirstOrDefaultAsync<BookDetailsViewModel>(sql, new { Id = id });

            Book.PdfUrl = readMode ? Book.PdfUrl : null;

            sql = $@"SELECT *  FROM 
                         BookAttachments ba
                         WHERE ba.BookId = @Id";
            Book.BookAttachments = (await connection.QueryAsync<BookAttachment>(sql, new { Id = id })).ToList();

            sql = $@"SELECT *  FROM 
                         BookFeatures ba
                         WHERE ba.BookId = @Id";
            Book.Features = (await connection.QueryAsync<BookFeature>(sql, new { Id = id })).Select(r => r.Title).ToList();

            if (Book == null) return NotFound(new { message = "Book not found" });
            return Ok(Book);
        }




        // ---------------------- CREATE BOOK ----------------------
        [HttpPost]
        [Authorize(Roles = "Admin,Librarian")]
        public async Task<IActionResult> Create([FromForm] BookDTO book)
        {
            var userId = int.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("Id").Value);

            using var connection = _context.CreateConnection();

            var sql = @"
            INSERT INTO Books 
            (Title, Price, DiscountPercent, PriceBeforeDiscount, CategoryId, PublisherId, AuthorId, Edition, Volume, ShortDescription, Details, Status, RegisteredBy, RegisterDate) 
            VALUES 
            (@Title, @Price, @DiscountPercent, @PriceBeforeDiscount, @CategoryId, @PublisherId, @AuthorId, @Edition, @Volume, @ShortDescription, @Details, @Status, @RegisteredBy, @RegisterDate);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
        ";

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
                RegisterDate = DateTime.UtcNow
            });

            // Save Cover Photo
            if (book.CoverPhoto != null)
            {
                var fileName = $"cover_{bookId}_{book.CoverPhoto.FileName}";
                var path = Path.Combine("wwwroot/uploads/covers", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var stream = System.IO.File.Create(path);
                await book.CoverPhoto.CopyToAsync(stream);
                await connection.ExecuteAsync("UPDATE Books SET CoverPhotoUrl=@url WHERE Id=@id",
                    new { url = $"/uploads/covers/{fileName}", id = bookId });
            }

            // Save PDF
            if (book.PdfFile != null)
            {
                var fileName = $"pdf_{bookId}_{book.PdfFile.FileName}";
                var path = Path.Combine("wwwroot/uploads/pdfs", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var stream = System.IO.File.Create(path);
                await book.PdfFile.CopyToAsync(stream);
                await connection.ExecuteAsync("UPDATE Books SET PdfUrl=@url WHERE Id=@id",
                    new { url = $"/uploads/pdfs/{fileName}", id = bookId });
            }

            // Save Features
            if (book.Features?.Any() == true)
            {
                foreach (var f in book.Features)
                {
                    await connection.ExecuteAsync(
                        "INSERT INTO BookFeatures (Title, BookId, Status) VALUES (@title, @bookId, 1)",
                        new { title = f, bookId });
                }
            }

            // Save Book Images
            if (book.bookAttachments?.Any() == true)
            {
                foreach (var img in book.bookAttachments)
                {
                    if (img.File != null)
                    {
                        var fileName = $"img_{bookId}_{img.File.FileName}";
                        var path = Path.Combine("wwwroot/uploads/images", fileName);
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        using var stream = System.IO.File.Create(path);
                        await img.File.CopyToAsync(stream);
                        await connection.ExecuteAsync(
                            "INSERT INTO BookAttachments (BookId, FileUrl) VALUES (@bookId, @url)",
                            new { bookId, url = $"/uploads/images/{fileName}" });
                    }
                }
            }

            return Ok(bookId);
        }

        // ---------------------- UPDATE BOOK ----------------------
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Librarian")]
        public async Task<IActionResult> Update(int id, [FromForm] BookDTO book)
        {
            using var connection = _context.CreateConnection();

            // Get existing book to check existing cover/PDF
            var existingBook = await connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT CoverPhotoUrl, PdfUrl FROM Books WHERE Id=@id", new { id });

            // Update main book info
            var sql = @"
            UPDATE Books SET
            Title=@Title, Price=@Price, DiscountPercent=@DiscountPercent, PriceBeforeDiscount=@PriceBeforeDiscount,
            CategoryId=@CategoryId, PublisherId=@PublisherId, AuthorId=@AuthorId,
            Edition=@Edition, Volume=@Volume, ShortDescription=@ShortDescription, Details=@Details,
            Status=@Status
            WHERE Id=@Id
        ";
            await connection.ExecuteAsync(sql, book);

            // Replace Cover Photo
            if (book.CoverPhoto != null)
            {
                // Delete old cover
                if (existingBook.CoverPhotoUrl != null)
                {
                    var oldPath = Path.Combine("wwwroot", existingBook.CoverPhotoUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                var fileName = $"cover_{id}_{book.CoverPhoto.FileName}";
                var path = Path.Combine("wwwroot/uploads/covers", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var stream = System.IO.File.Create(path);
                await book.CoverPhoto.CopyToAsync(stream);

                await connection.ExecuteAsync("UPDATE Books SET CoverPhotoUrl=@url WHERE Id=@id",
                    new { url = $"/uploads/covers/{fileName}", id });
            }

            // Replace PDF
            if (book.PdfFile != null)
            {
                // Delete old PDF
                if (existingBook.PdfUrl != null)
                {
                    var oldPath = Path.Combine("wwwroot", existingBook.PdfUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                var fileName = $"pdf_{id}_{book.PdfFile.FileName}";
                var path = Path.Combine("wwwroot/uploads/pdfs", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var stream = System.IO.File.Create(path);
                await book.PdfFile.CopyToAsync(stream);

                await connection.ExecuteAsync("UPDATE Books SET PdfUrl=@url WHERE Id=@id",
                    new { url = $"/uploads/pdfs/{fileName}", id });
            }

            // Replace Features
            await connection.ExecuteAsync("DELETE FROM BookFeatures WHERE BookId=@id", new { id });
            if (book.Features?.Any() == true)
            {
                foreach (var f in book.Features)
                {
                    await connection.ExecuteAsync(
                        "INSERT INTO BookFeatures (Title, BookId, Status) VALUES (@title, @bookId, 1)",
                        new { title = f, bookId = id });
                }
            }

            // Replace Book Images
            var existingImages = (await connection.QueryAsync<BookAttachmentDTO>(
                "SELECT * FROM BookAttachments WHERE BookId=@id", new { id })).ToList();

            var keepIds = book.bookAttachments?.Where(x => x.Id != 0).Select(x => x.Id).ToList() ?? new List<int>();
            var toDelete = existingImages.Where(x => !keepIds.Contains(x.Id)).ToList();

            foreach (var img in toDelete)
            {
                var path = Path.Combine("wwwroot", img.FileUrl.TrimStart('/'));
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                await connection.ExecuteAsync("DELETE FROM BookAttachments WHERE Id=@id", new { id = img.Id });
            }

            // Add new images
            if (book.bookAttachments?.Any() == true)
            {
                foreach (var img in book.bookAttachments.Where(x => x.File != null))
                {
                    var fileName = $"img_{id}_{img.File.FileName}";
                    var path = Path.Combine("wwwroot/uploads/images", fileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    using var stream = System.IO.File.Create(path);
                    await img.File.CopyToAsync(stream);
                    await connection.ExecuteAsync(
                        "INSERT INTO BookAttachments (BookId, FileUrl) VALUES (@bookId, @url)",
                        new { bookId = id, url = $"/uploads/images/{fileName}" });
                }
            }

            return NoContent();
        }

        // ---------------------- DELETE BOOK ----------------------
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Librarian")]
        public async Task<IActionResult> Delete(int id)
        {
            using var connection = _context.CreateConnection();

            // Delete cover and PDF
            var book = await connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT CoverPhotoUrl, PdfUrl FROM Books WHERE Id=@id", new { id });

            if (book?.CoverPhotoUrl != null)
            {
                var path = Path.Combine("wwwroot", book.CoverPhotoUrl.TrimStart('/'));
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }

            if (book?.PdfUrl != null)
            {
                var path = Path.Combine("wwwroot", book.PdfUrl.TrimStart('/'));
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }

            // Delete book images
            var images = (await connection.QueryAsync<BookAttachmentDTO>(
                "SELECT * FROM BookAttachments WHERE BookId=@id", new { id })).ToList();

            foreach (var img in images)
            {
                var path = Path.Combine("wwwroot", img.FileUrl.TrimStart('/'));
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }

            await connection.ExecuteAsync("DELETE FROM BookAttachments WHERE BookId=@id", new { id });
            await connection.ExecuteAsync("DELETE FROM BookFeatures WHERE BookId=@id", new { id });
            await connection.ExecuteAsync("DELETE FROM UserActions WHERE BookId=@id", new { id });
            await connection.ExecuteAsync("DELETE FROM Books WHERE Id=@id", new { id });

            return Ok(new { message = "Book deleted successfully" });
        }

        [HttpPost("user-action")]
        public async Task<IActionResult> UserAction([FromBody] UserActionDTO request)
        {

            var userId = int.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("Id").Value);
            using var connection = _context.CreateConnection();

            var sql = @"INSERT INTO UserActions (UserId, BookId, ActionType, ActionTime) VALUES (@UserId, @BookId, @ActionType, @ActionTime);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = await connection.ExecuteScalarAsync<int>(sql, new UserAction() {
                 UserId = userId,
                 BookId = request.BookId,
                 ActionType = request.ActionType,
                 ActionTime = DateTime.UtcNow,
            });


            return Ok(id);
        }


    }
}
