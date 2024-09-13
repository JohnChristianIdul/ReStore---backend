using Microsoft.AspNetCore.Mvc;
using ReStore___backend.Dtos;
using ReStore___backend.Services.Interfaces;
using System.Threading.Tasks;

namespace ReStore___backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IDataService _dataService;

        public AuthController(IDataService dataService)
        {
            _dataService = dataService;
        }

        [HttpPost("/signup")]
        public async Task<IActionResult> SignUp([FromBody] SignUpDTO signUpDto)
        {
            if (signUpDto == null)
                return BadRequest(new { error = "Invalid sign-up data." });

            try
            {
                // Call service method for sign-up
                var result = await _dataService.SignUp(
                    signUpDto.Email,
                    signUpDto.Name,
                    signUpDto.Password,
                    signUpDto.PhoneNumber,
                    signUpDto.Username
                );

                if (result.StartsWith("Error"))
                    return BadRequest(new { error = result });

                return Ok(new { message = result });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        [HttpPost("/login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO loginDto)
        {
            if (loginDto == null)
                return BadRequest(new { error = "Invalid login data." });

            try
            {
                // Call service method for login
                var token = await _dataService.Login(loginDto.Email, loginDto.Password);

                if (token.StartsWith("Error"))
                    return Unauthorized(new { error = token });

                return Ok(new { token });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }
    }
}
