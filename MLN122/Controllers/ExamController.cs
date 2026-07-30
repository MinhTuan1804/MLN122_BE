using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLN122.Data;
using MLN122.Models;

namespace MLN122.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamController : ControllerBase
{
    private readonly AppDbContext _context;

    public ExamController(AppDbContext context)
    {
        _context = context;
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(claim, out var userId)) return userId;
        return null;
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartExam([FromQuery] int questionCount = 60, [FromQuery] int timeLimitMinutes = 60)
    {
        var totalAvailable = await _context.Questions.CountAsync();
        if (totalAvailable == 0)
        {
            return BadRequest(new { message = "Chưa có dữ liệu câu hỏi trong hệ thống." });
        }

        var countToTake = Math.Min(Math.Max(1, questionCount), totalAvailable);
        var timeLimit = Math.Max(1, timeLimitMinutes);
        
        // Randomly select questions
        var randomQuestions = await _context.Questions
            .Include(q => q.Options)
            .OrderBy(r => EF.Functions.Random())
            .Take(countToTake)
            .ToListAsync();

        var examSession = new
        {
            SessionId = Guid.NewGuid().ToString(),
            StartedAt = DateTime.UtcNow,
            TimeLimitMinutes = timeLimit,
            TotalQuestions = randomQuestions.Count,
            Questions = randomQuestions.Select((q, index) => new
            {
                OrderIndex = index + 1,
                q.Id,
                q.QuestionNum,
                q.Content,
                Options = q.Options.OrderBy(o => o.Key).Select(o => new { o.Id, o.Key, o.Content })
            })
        };

        return Ok(examSession);
    }

    public record UserAnswerItemDto(int QuestionId, string UserAnswer, bool IsFlagged);
    public record SubmitExamDto(int TimeSpentSeconds, List<UserAnswerItemDto> Answers);

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitExam([FromBody] SubmitExamDto dto)
    {
        if (dto.Answers == null || dto.Answers.Count == 0)
        {
            return BadRequest(new { message = "Bài thi chưa có câu trả lời nào." });
        }

        var qIds = dto.Answers.Select(a => a.QuestionId).Distinct().ToList();
        var dbQuestions = await _context.Questions
            .Include(q => q.Options)
            .Where(q => qIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id);

        int correctCount = 0;
        var details = new List<ExamDetail>();

        foreach (var item in dto.Answers)
        {
            if (!dbQuestions.TryGetValue(item.QuestionId, out var question)) continue;

            var normalizedUserAns = (item.UserAnswer ?? "").Trim().ToUpper();
            var normalizedCorrectAns = (question.CorrectAnswer ?? "").Trim().ToUpper();

            var isCorrect = false;
            if (normalizedUserAns == normalizedCorrectAns)
            {
                isCorrect = true;
            }
            else
            {
                var userParts = normalizedUserAns.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).OrderBy(s => s);
                var correctParts = normalizedCorrectAns.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).OrderBy(s => s);
                isCorrect = userParts.SequenceEqual(correctParts);
            }

            if (isCorrect) correctCount++;

            details.Add(new ExamDetail
            {
                QuestionId = item.QuestionId,
                UserAnswer = normalizedUserAns,
                CorrectAnswer = normalizedCorrectAns,
                IsCorrect = isCorrect,
                IsFlagged = item.IsFlagged
            });
        }

        double score = Math.Round((double)correctCount / dto.Answers.Count * 10.0, 2);
        bool isPassed = score >= 5.0;

        var userId = GetCurrentUserId();
        ExamAttempt? attempt = null;

        if (userId.HasValue)
        {
            attempt = new ExamAttempt
            {
                UserId = userId.Value,
                StartedAt = DateTime.UtcNow.AddSeconds(-dto.TimeSpentSeconds),
                CompletedAt = DateTime.UtcNow,
                TotalQuestions = dto.Answers.Count,
                CorrectCount = correctCount,
                Score = score,
                IsPassed = isPassed,
                TimeSpentSeconds = dto.TimeSpentSeconds,
                Details = details
            };

            _context.ExamAttempts.Add(attempt);
            await _context.SaveChangesAsync();
        }

        var resultDetails = details.Select(d =>
        {
            dbQuestions.TryGetValue(d.QuestionId, out var q);
            return new
            {
                d.QuestionId,
                QuestionNum = q?.QuestionNum ?? "",
                Content = q?.Content ?? "",
                Explanation = q?.Explanation ?? "",
                Options = q?.Options.OrderBy(o => o.Key).Select(o => new { o.Id, o.Key, o.Content }),
                d.UserAnswer,
                d.CorrectAnswer,
                d.IsCorrect,
                d.IsFlagged
            };
        });

        return Ok(new
        {
            ExamAttemptId = attempt?.Id,
            TotalQuestions = dto.Answers.Count,
            CorrectCount = correctCount,
            WrongCount = dto.Answers.Count - correctCount,
            Score = score,
            IsPassed = isPassed,
            TimeSpentSeconds = dto.TimeSpentSeconds,
            SubmittedAt = DateTime.UtcNow,
            Details = resultDetails
        });
    }

    [HttpGet("history")]
    [Authorize]
    public async Task<IActionResult> GetExamHistory()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var attempts = await _context.ExamAttempts
            .Where(e => e.UserId == userId.Value)
            .OrderByDescending(e => e.CompletedAt)
            .Select(e => new
            {
                e.Id,
                e.StartedAt,
                e.CompletedAt,
                e.TotalQuestions,
                e.CorrectCount,
                e.Score,
                e.IsPassed,
                e.TimeSpentSeconds
            })
            .ToListAsync();

        return Ok(attempts);
    }

    [HttpGet("history/{id}")]
    [Authorize]
    public async Task<IActionResult> GetExamAttemptById(int id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var attempt = await _context.ExamAttempts
            .Include(e => e.Details)
                .ThenInclude(d => d.Question)
                    .ThenInclude(q => q!.Options)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId.Value);

        if (attempt == null) return NotFound();

        return Ok(new
        {
            attempt.Id,
            attempt.StartedAt,
            attempt.CompletedAt,
            attempt.TotalQuestions,
            attempt.CorrectCount,
            attempt.Score,
            attempt.IsPassed,
            attempt.TimeSpentSeconds,
            Details = attempt.Details.Select(d => new
            {
                d.QuestionId,
                QuestionNum = d.Question?.QuestionNum ?? "",
                Content = d.Question?.Content ?? "",
                Explanation = d.Question?.Explanation ?? "",
                Options = d.Question?.Options.OrderBy(o => o.Key).Select(o => new { o.Id, o.Key, o.Content }),
                d.UserAnswer,
                d.CorrectAnswer,
                d.IsCorrect,
                d.IsFlagged
            })
        });
    }
}
