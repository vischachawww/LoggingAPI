
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;


    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;

        public AuthController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost("token")]
        [AllowAnonymous]  // 🔑 this ensures login endpoint doesn’t need a token
        public IActionResult GenerateToken([FromBody] AuthRequest request)
        {
            if (string.IsNullOrEmpty(request.ApplicationName))
                return BadRequest("Missing ApplicationName");

            var claims = new[]
            {
                new Claim("ApplicationName", request.ApplicationName)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddYears(10), //lasts 10 yrs
                signingCredentials: creds);

            return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
        }
    }

    public class AuthRequest
    {
        public string ApplicationName { get; set; }
    }


