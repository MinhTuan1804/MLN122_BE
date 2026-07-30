using System.Security.Claims;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLN122.Data;
using MLN122.Models;
using MLN122.Services;

namespace MLN122.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly JwtService _jwtService;

    public AuthController(AppDbContext context, JwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    public record RegisterDto(string Username, string Email, string Password);
    public record LoginDto(string UsernameOrEmail, string Password);
    public record AuthResponseDto(string Token, int UserId, string Username, string Email, DateTime ExpiresAt);

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new { message = "Vui lòng nhập đầy đủ Tên người dùng, Email và Mật khẩu." });
        }

        var normalizedUsername = dto.Username.Trim();
        var normalizedEmail = dto.Email.Trim().ToLower();

        if (await _context.Users.AnyAsync(u => u.Username.ToLower() == normalizedUsername.ToLower()))
        {
            return BadRequest(new { message = "Tên người dùng đã tồn tại." });
        }

        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
        {
            return BadRequest(new { message = "Email này đã được đăng ký." });
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        var user = new User
        {
            Username = normalizedUsername,
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = _jwtService.GenerateToken(user);
        return Ok(new AuthResponseDto(token, user.Id, user.Username, user.Email, DateTime.UtcNow.AddDays(7)));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.UsernameOrEmail) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new { message = "Vui lòng nhập Tên người dùng/Email và Mật khẩu." });
        }

        var query = dto.UsernameOrEmail.Trim().ToLower();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == query || u.Email.ToLower() == query);

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return BadRequest(new { message = "Tài khoản hoặc mật khẩu không chính xác." });
        }

        var token = _jwtService.GenerateToken(user);
        return Ok(new AuthResponseDto(token, user.Id, user.Username, user.Email, DateTime.UtcNow.AddDays(7)));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var examCount = await _context.ExamAttempts.CountAsync(e => e.UserId == userId);
        var avgScore = examCount > 0 ? await _context.ExamAttempts.Where(e => e.UserId == userId).AverageAsync(e => e.Score) : 0;
        var starredCount = await _context.UserProgresses.CountAsync(p => p.UserId == userId && p.IsStarred);
        var masteredCount = await _context.UserProgresses.CountAsync(p => p.UserId == userId && p.IsMastered);

        return Ok(new
        {
            user.Id,
            user.Username,
            user.Email,
            user.CreatedAt,
            Stats = new
            {
                TotalExams = examCount,
                AverageScore = Math.Round(avgScore, 2),
                StarredQuestions = starredCount,
                MasteredQuestions = masteredCount
            }
        });
    }
}
