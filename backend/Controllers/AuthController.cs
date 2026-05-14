using FaceAccessController.Api.Contracts;
using FaceAccessController.Api.Data;
using FaceAccessController.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceAccessController.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    JwtTokenService jwtTokenService,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var user = await db.Users.FirstOrDefaultAsync(x => x.Username == request.Username, ct);
            if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized("Invalid credentials.");
            }

            var token = jwtTokenService.CreateToken(user);
            return Ok(new LoginResponse(token, user.Username, user.Role));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login failed due to backend startup/database issue.");
            return StatusCode(StatusCodes.Status500InternalServerError, "Login failed. Please verify database and server configuration.");
        }
    }
}
