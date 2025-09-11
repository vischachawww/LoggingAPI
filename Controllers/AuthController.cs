
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

        //jwt token generator
        [HttpPost("token")]
        [AllowAnonymous]  //public endpoint : anyone can request a token
        public IActionResult GenerateToken([FromBody] AuthRequest request)
        {
            if (string.IsNullOrEmpty(request.ApplicationName))
                return BadRequest("Missing ApplicationName"); //400

            //claims = information to store inside JWT
            var claims = new[]
            {
                new Claim("ApplicationName", request.ApplicationName)
            };
            //setup signing key and credentials .zzzzz
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddYears(10), //lasts 10 yrs
                signingCredentials: creds);

            //convert the raw token into a string xxx.yyy.zzz and return to client in Json
            return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
        }
    }

    public class AuthRequest
    {
        public string ApplicationName { get; set; }
    }


