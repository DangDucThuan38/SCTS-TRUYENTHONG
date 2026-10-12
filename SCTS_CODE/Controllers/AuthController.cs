//using Microsoft.AspNetCore.Mvc;
//using Microsoft.IdentityModel.Tokens;
//using ngay_1.DTOs;
//using System.Security.Claims;
//using System.Security.Cryptography;
//using Microsoft.AspNetCore.Http;
//using System.IdentityModel.Tokens.Jwt;

//namespace ngay_1.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class AuthController : Controller
//    {
//        public static User user = new User();

//        private readonly IConfiguration _configuration;

//        public AuthController(IConfiguration configuration)
//        {
//            _configuration = configuration;
//        }

//        [HttpPost("register")]
//        public async Task<ActionResult<User>> Register(UserDto request)
//        {
//            CreatePasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

//            user.UserName = request.Username;
//            user.PasswordHash = Convert.ToBase64String(passwordHash);
//            user.PasswordSalt = Convert.ToBase64String(passwordSalt);

//            return Ok(user);
//        }

//        [HttpPost("login")]
//        public async Task<ActionResult<string>> Login(UserDto request)
//        {
//            if (user.UserName != request.Username)
//            {
//                return BadRequest("User not found.");
//            }

//            byte[] savedPasswordHash = Convert.FromBase64String(user.PasswordHash);
//            byte[] savedPasswordSalt = Convert.FromBase64String(user.PasswordSalt);

//            if (!VerifyPasswordHash(request.Password, savedPasswordHash, savedPasswordSalt))
//            {
//                return BadRequest("Wrong password.");
//            }

//            try
//            {
//                string token = CreateToken(user);
//                return Ok(token);
//            }
//            catch (Exception ex)
//            {
//                return BadRequest(ex.Message);
//            }
//        }

//        private string CreateToken(User user)
//        {
//            List<Claim> claims = new List<Claim>
//            {
//                new Claim(ClaimTypes.Name, user.UserName)
//            };

//            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(
//                _configuration.GetSection("AppSettings:Token").Value));

//            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

//            var token = new JwtSecurityToken(
//                claims: claims,
//                expires: DateTime.Now.AddDays(1),
//                signingCredentials: creds);

//            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

//            return jwt;
//        }

//        private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
//        {
//            using (var hmac = new HMACSHA512())
//            {
//                passwordSalt = hmac.Key;
//                passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
//            }
//        }

//        private bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
//        {
//            using (var hmac = new HMACSHA512(passwordSalt))
//            {
//                var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
//                return computedHash.SequenceEqual(passwordHash);
//            }
//        }
//    }
//}