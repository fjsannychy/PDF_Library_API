using Blog.Data;
using Dapper;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MimeKit;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Ocsp;
using PDFLibrary.Models.DTOs;
using PDFLibrary.Models.Entities;
using PDFLibrary.Models.Enums;
using PDFLibrary.Models.ViewModels;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PDFLibrary.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly DapperContext _context;
        private readonly IConfiguration _configuration;
        private readonly EmailSettings _settings;

        public UsersController(DapperContext context, IConfiguration configuration, IOptions<EmailSettings> settings)
        {
            _context = context;
            _configuration = configuration;
            _settings = settings.Value;
        }


        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginAsync(LoginRequest request)
        {
            using var connection = _context.CreateConnection();
            var sql = "SELECT * FROM users WHERE username = @Username and passwordHash = @Password and Status = 1";
            var user = await connection.QueryFirstOrDefaultAsync<User>(sql,
                                                 new
                                                 {
                                                     Username = request.Username,
                                                     Password = GetMd5Hash(request.Password),
                                                 });

            if (user == null)
                return Unauthorized("Invalid credentials");

            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken(user.Id);


            var insertRefreshTokenSql = @"INSERT INTO RefreshTokens (Token, UserId, ExpiresAt,IsRevoked) 
                                           VALUES (@Token, @UserId, @ExpiresAt, @IsRevoked);";

            await connection.ExecuteScalarAsync(insertRefreshTokenSql, refreshToken);

            return Ok(new
            {
                accessToken,
                refreshToken = refreshToken.Token,
                username = user.Username,
                role = user.Role
            });
        }

        private string GenerateAccessToken(User user)
        {
            var jwt = _configuration.GetSection("Jwt");

            var claims = new[]
            {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("Id", user.Id.ToString())
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["Key"]!)
            );

            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    int.Parse(jwt["AccessTokenExpiryMinutes"]!)
                ),
                signingCredentials: new SigningCredentials(
                    key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private RefreshToken GenerateRefreshToken(int Id)
        {
            var jwt = _configuration.GetSection("Jwt");

            var randomBytes = RandomNumberGenerator.GetBytes(64);

            return new RefreshToken
            {
                UserId = Id,
                Token = Convert.ToBase64String(randomBytes),
                ExpiresAt = DateTime.UtcNow.AddDays(
                    int.Parse(jwt["RefreshTokenExpiryDays"]!)
                ),
                IsRevoked = false
            };
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshAsync(TokenRequest request)
        {

            using var connection = _context.CreateConnection();
            var sql = "SELECT * FROM RefreshTokens WHERE token = @Token and IsRevoked = 0 and  ExpiresAt > @CurrentUTC ";
            var storedToken = await connection.QueryFirstOrDefaultAsync<RefreshToken>(sql,
                                                 new
                                                 {
                                                     Token = request.RefreshToken,
                                                     CurrentUTC = DateTime.UtcNow
                                                 });

            if (storedToken == null)
                return Unauthorized("Invalid refresh token");

            var userSql = "SELECT * FROM users WHERE Id = @Id";
            var user = await connection.QueryFirstOrDefaultAsync<User>(userSql, new { Id = storedToken.UserId });
            if (user == null)
                return Unauthorized();

            var deleteRefreshTokenSql = "DELETE FROM RefreshTokens WHERE Token = @Token";
            var affectedRows = await connection.ExecuteAsync(deleteRefreshTokenSql, new { Token = storedToken.Token });

            var newRefreshToken = GenerateRefreshToken(user.Id);

            var insertRefreshTokenSql = @"INSERT INTO RefreshTokens (Token, UserId, ExpiresAt,IsRevoked) 
                                           VALUES (@Token, @UserId, @ExpiresAt, @IsRevoked);
                                           SELECT CAST(SCOPE_IDENTITY() AS NVARCHAR(MAX));";

            await connection.ExecuteScalarAsync(insertRefreshTokenSql, newRefreshToken);

            var newAccessToken = GenerateAccessToken(user);

            return Ok(new
            {
                accessToken = newAccessToken,
                refreshToken = newRefreshToken.Token
            });
        }

        [HttpPost("logout")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout(TokenRequest request)
        {
            using var connection = _context.CreateConnection();
            var deleteRefreshTokenSql = "DELETE FROM RefreshTokens WHERE Token = @Token";
            var affectedRows = await connection.ExecuteAsync(deleteRefreshTokenSql, new { Token = request.RefreshToken });

            return Ok();
        }

        // 1. GET ALL USERS: api/users
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll()
        {
            using var connection = _context.CreateConnection();
            var sql = "SELECT * FROM users";
            var users = await connection.QueryAsync<UserViewModel>(sql);
            return Ok(users);
        }
        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] UserFilter filter)
        {
            using var connection = _context.CreateConnection();

            var sql = @"
        SELECT *
        FROM users
        WHERE Username LIKE @SearchText OR Fullname LIKE @SearchText
        ORDER BY Id DESC
        OFFSET @Offset ROWS
        FETCH NEXT @PageSize ROWS ONLY;
    ";

            var parameters = new DynamicParameters();
            var offset = (filter.PageNumber - 1) * filter.PageSize;

            parameters.Add("SearchText", $"%{filter.Search}%");
            parameters.Add("Offset", offset);
            parameters.Add("PageSize", filter.PageSize);

            // Query users
            var users = await connection.QueryAsync<User>(sql, parameters);

            // Total count for pagination
            var countSql = @"
        SELECT COUNT(*) 
        FROM users
        WHERE Username LIKE @SearchText OR Fullname LIKE @SearchText
    ";
            var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

            // Return wrapped in view model
            return Ok(new UserViewModel
            {
                Users = users,
                TotalCount = totalCount
            });
        }

        // 2. GET BY ID: api/users/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            using var connection = _context.CreateConnection();
            var sql = "SELECT * FROM users WHERE Id = @Id";
            var user = await connection.QueryFirstOrDefaultAsync<UserViewModel>(sql, new { Id = id });

            if (user == null) return NotFound(new { message = "User not found" });
            return Ok(user);
        }

        // 2. GET BY ID: api/users/5
        [HttpPost("verify-user")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyUser(VerifyUserDTO request)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(Decrypt(request.Token));

                using var connection = _context.CreateConnection();
                var sql = "SELECT * FROM users WHERE Id = @Id and Status = 0 ";
                var user = await connection.QueryFirstOrDefaultAsync<User>(sql, new { Id = data["Id"] });

                if (user == null) return BadRequest("Verification Failed");

                sql = @"UPDATE users 
                        SET status = @Status
                        WHERE Id = @Id";

                var affectedRows = await connection.ExecuteAsync(sql, new
                {
                    Status = UserStatus.Active,
                    Id = user.Id
                });

                if (affectedRows == 0) return BadRequest("Verification Failed");

            }
            catch(Exception ex)
            {
                return BadRequest("Verification Failed");
            }


            return Ok("Verification Succesfull!");
        }


        static string GetMd5Hash(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2")); // convert to hex
                }

                return sb.ToString();
            }
        }

        // 3. CREATE USER: api/users
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RegisterUserRequest user)
        {
            if (string.IsNullOrEmpty(user.Username)) return BadRequest("Username is required");

            using var connection = _context.CreateConnection();

            var sql = "SELECT * FROM users WHERE username = @Username";
            var userExist = await connection.QueryFirstOrDefaultAsync<User>(sql,
                                                 new
                                                 {
                                                     Username = user.Username
                                                 });

            if (userExist != null)
                return BadRequest("User Exsists!!");

            sql = @"INSERT INTO users (username, fullname, passwordHash, role, status) 
                        VALUES (@Username, @Fullname, @Password, @Role, @Status);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = await connection.ExecuteScalarAsync<int>(sql, new
            {
                Username = user.Username,
                Fullname = user.Fullname,
                Password = GetMd5Hash(user.Password),
                Role = Role.General.ToString(),
                Status = UserStatus.Inactive
            });
            user.Id = id;

            await SendVerificationEmail(user.Username, 
                                    Encrypt(JsonConvert.SerializeObject(new { Id = user.Id, Date = DateTime.UtcNow }))
                                 );

            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }

        private string Encrypt(string plainText)
        {
            var encryption = _configuration.GetSection("Encryption");

            using var aes = Aes.Create();

            // Derive key + IV from password
            var salt = Encoding.UTF8.GetBytes(encryption["Salt"]!); // keep constant
            var pdb = new Rfc2898DeriveBytes(encryption["Key"]!, salt, 10000, HashAlgorithmName.SHA256);

            aes.Key = pdb.GetBytes(32); // 256-bit key
            aes.IV = pdb.GetBytes(16);  // 128-bit IV

            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            using var sw = new StreamWriter(cs);

            sw.Write(plainText);
            sw.Close();

            return Convert.ToBase64String(ms.ToArray());
        }


        private string Decrypt(string cipherText)
        {

            var encryption = _configuration.GetSection("Encryption");

            using var aes = Aes.Create();

            var salt = Encoding.UTF8.GetBytes(encryption["Salt"]!);
            var pdb = new Rfc2898DeriveBytes(encryption["Key"]!, salt, 10000, HashAlgorithmName.SHA256);

            aes.Key = pdb.GetBytes(32);
            aes.IV = pdb.GetBytes(16);

            var buffer = Convert.FromBase64String(cipherText);

            using var ms = new MemoryStream(buffer);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);

            return sr.ReadToEnd();
        }


        public async Task SendVerificationEmail(string toEmail, string token)
        {
            token = WebUtility.UrlEncode(token);
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("My App", _settings.Email));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = "Verify Email";

            var link = $"{_settings.VerficationURL}/{token}";

            message.Body = new TextPart("html")
            {
                Text = $"Verify your PDF Library Account: <a href='{link}'>Click to Verify</a>"
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.Host, _settings.Port, false);

            await client.AuthenticateAsync(_settings.Email, _settings.AppPassword);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        // 4. UPDATE USER: api/users/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] User user)
        {
            using var connection = _context.CreateConnection();
            var sql = @"UPDATE users 
                        SET fullname = @Fullname, passwordHash = @Password, role = @Role, status = @Status
                        WHERE Id = @Id";

            var affectedRows = await connection.ExecuteAsync(sql, new
            {
                Username = user.Username,
                Fullname = user.Fullname,
                Password = GetMd5Hash(user.Password),
                Role = user.Role,
                Status = user.Status,
                Id = id
            });

            if (affectedRows == 0) return NotFound();
            return Ok(new { message = "User updated successfully" });
        }

        // 5. DELETE USER: api/users/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            using var connection = _context.CreateConnection();
            var sql = "DELETE FROM users WHERE Id = @Id";
            var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });

            if (affectedRows == 0) return NotFound();
            return Ok(new { message = "User deleted successfully" });
        }
    }
}
