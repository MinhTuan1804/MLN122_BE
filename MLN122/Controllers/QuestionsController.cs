using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLN122.Data;
using MLN122.Models;

namespace MLN122.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuestionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public QuestionsController(AppDbContext context)
    {
        _context = context;
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(claim, out var userId)) return userId;
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> GetQuestions(
        [FromQuery] string? search,
        [FromQuery] string? filterType, // "all" | "starred" | "wrong" | "mastered"
        [FromQuery] bool? onlyStarred,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 539)
    {
        var userId = GetCurrentUserId();
        var query = _context.Questions.Include(q => q.Options).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(q => q.Content.ToLower().Contains(searchLower) || q.QuestionNum.ToLower().Contains(searchLower));
        }

        var effectiveFilter = filterType ?? (onlyStarred == true ? "starred" : "all");

        if (userId.HasValue)
        {
            if (effectiveFilter == "starred")
            {
                var starredQIds = _context.UserProgresses
                    .Where(up => up.UserId == userId.Value && up.IsStarred)
                    .Select(up => up.QuestionId);

                query = query.Where(q => starredQIds.Contains(q.Id));
            }
            else if (effectiveFilter == "wrong")
            {
                var masteredQIds = _context.UserProgresses
                    .Where(up => up.UserId == userId.Value && up.IsMastered)
                    .Select(up => up.QuestionId);

                var wrongProgressQIds = _context.UserProgresses
                    .Where(up => up.UserId == userId.Value && up.CorrectStreak == 0 && !up.IsMastered)
                    .Select(up => up.QuestionId);

                var wrongExamQIds = _context.ExamDetails
                    .Where(ed => ed.ExamAttempt != null && ed.ExamAttempt.UserId == userId.Value && !ed.IsCorrect && !masteredQIds.Contains(ed.QuestionId))
                    .Select(ed => ed.QuestionId);

                var wrongQIds = wrongProgressQIds.Union(wrongExamQIds).Distinct();
                query = query.Where(q => wrongQIds.Contains(q.Id));
            }
            else if (effectiveFilter == "mastered")
            {
                var masteredQIds = _context.UserProgresses
                    .Where(up => up.UserId == userId.Value && up.IsMastered)
                    .Select(up => up.QuestionId);

                query = query.Where(q => masteredQIds.Contains(q.Id));
            }
        }

        var totalItems = await query.CountAsync();
        var questions = await query
            .OrderBy(q => q.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        List<int> starredIds = new();
        List<int> masteredIds = new();

        if (userId.HasValue)
        {
            var qIds = questions.Select(q => q.Id).ToList();
            var progresses = await _context.UserProgresses
                .Where(p => p.UserId == userId.Value && qIds.Contains(p.QuestionId))
                .ToListAsync();

            starredIds = progresses.Where(p => p.IsStarred).Select(p => p.QuestionId).ToList();
            masteredIds = progresses.Where(p => p.IsMastered).Select(p => p.QuestionId).ToList();
        }

        var result = questions.Select(q => new
        {
            q.Id,
            q.QuestionNum,
            q.Content,
            q.CorrectAnswer,
            q.Explanation,
            Options = q.Options.OrderBy(o => o.Key).Select(o => new { o.Id, o.Key, o.Content }),
            IsStarred = starredIds.Contains(q.Id),
            IsMastered = masteredIds.Contains(q.Id)
        });

        return Ok(new
        {
            TotalItems = totalItems,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
            Data = result
        });
    }

    [HttpGet("user-state")]
    [Authorize]
    public async Task<IActionResult> GetUserState()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        var starredCount = await _context.UserProgresses.CountAsync(up => up.UserId == userId.Value && up.IsStarred);
        
        var masteredQIds = _context.UserProgresses
            .Where(up => up.UserId == userId.Value && up.IsMastered)
            .Select(up => up.QuestionId);

        var wrongProgressQIds = _context.UserProgresses
            .Where(up => up.UserId == userId.Value && up.CorrectStreak == 0 && !up.IsMastered)
            .Select(up => up.QuestionId);

        var wrongExamQIds = _context.ExamDetails
            .Where(ed => ed.ExamAttempt != null && ed.ExamAttempt.UserId == userId.Value && !ed.IsCorrect && !masteredQIds.Contains(ed.QuestionId))
            .Select(ed => ed.QuestionId);

        var wrongCount = await wrongProgressQIds.Union(wrongExamQIds).Distinct().CountAsync();
        var masteredCount = await _context.UserProgresses.CountAsync(up => up.UserId == userId.Value && up.IsMastered);

        return Ok(new
        {
            user.LastQuestionIndex,
            user.LastStudyMode,
            user.LastFilterType,
            StarredCount = starredCount,
            WrongCount = wrongCount,
            MasteredCount = masteredCount
        });
    }

    public record UpdateUserStateDto(int LastQuestionIndex, string LastStudyMode, string? LastFilterType);

    [HttpPost("user-state")]
    [Authorize]
    public async Task<IActionResult> UpdateUserState([FromBody] UpdateUserStateDto dto)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        user.LastQuestionIndex = Math.Max(0, dto.LastQuestionIndex);
        if (!string.IsNullOrWhiteSpace(dto.LastStudyMode))
        {
            user.LastStudyMode = dto.LastStudyMode;
        }
        if (!string.IsNullOrWhiteSpace(dto.LastFilterType))
        {
            user.LastFilterType = dto.LastFilterType;
        }

        await _context.SaveChangesAsync();
        return Ok(new { user.LastQuestionIndex, user.LastStudyMode, user.LastFilterType });
    }

    [HttpPost("user-progress/reset")]
    [Authorize]
    public async Task<IActionResult> ResetUserProgress()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        // 1. Remove all UserProgress records for this user
        var userProgresses = await _context.UserProgresses.Where(up => up.UserId == userId.Value).ToListAsync();
        _context.UserProgresses.RemoveRange(userProgresses);

        // 2. Remove all ExamAttempts (and cascade ExamDetails) for this user
        var examAttempts = await _context.ExamAttempts.Where(ea => ea.UserId == userId.Value).ToListAsync();
        _context.ExamAttempts.RemoveRange(examAttempts);

        // 3. Reset User position state
        var user = await _context.Users.FindAsync(userId.Value);
        if (user != null)
        {
            user.LastQuestionIndex = 0;
            user.LastStudyMode = "flashcard";
            user.LastFilterType = "all";
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Reset toàn bộ tiến trình học và lịch sử bài thi thành công." });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetQuestionById(int id)
    {
        var q = await _context.Questions.Include(q => q.Options).FirstOrDefaultAsync(q => q.Id == id);
        if (q == null) return NotFound();

        var userId = GetCurrentUserId();
        bool isStarred = false;
        bool isMastered = false;

        if (userId.HasValue)
        {
            var progress = await _context.UserProgresses.FirstOrDefaultAsync(p => p.UserId == userId.Value && p.QuestionId == id);
            if (progress != null)
            {
                isStarred = progress.IsStarred;
                isMastered = progress.IsMastered;
            }
        }

        return Ok(new
        {
            q.Id,
            q.QuestionNum,
            q.Content,
            q.CorrectAnswer,
            q.Explanation,
            Options = q.Options.OrderBy(o => o.Key).Select(o => new { o.Id, o.Key, o.Content }),
            IsStarred = isStarred,
            IsMastered = isMastered
        });
    }

    [HttpPost("{id}/star")]
    [Authorize]
    public async Task<IActionResult> ToggleStar(int id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var question = await _context.Questions.FindAsync(id);
        if (question == null) return NotFound();

        var progress = await _context.UserProgresses.FirstOrDefaultAsync(p => p.UserId == userId.Value && p.QuestionId == id);

        if (progress == null)
        {
            progress = new UserProgress
            {
                UserId = userId.Value,
                QuestionId = id,
                IsStarred = true,
                LastReviewedAt = DateTime.UtcNow
            };
            _context.UserProgresses.Add(progress);
        }
        else
        {
            progress.IsStarred = !progress.IsStarred;
            progress.LastReviewedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(new { QuestionId = id, IsStarred = progress.IsStarred });
    }

    public record RecordAnswerDto(bool IsCorrect);

    [HttpPost("{id}/record")]
    [Authorize]
    public async Task<IActionResult> RecordAnswer(int id, [FromBody] RecordAnswerDto dto)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var progress = await _context.UserProgresses.FirstOrDefaultAsync(p => p.UserId == userId.Value && p.QuestionId == id);
        if (progress == null)
        {
            progress = new UserProgress
            {
                UserId = userId.Value,
                QuestionId = id,
                CorrectStreak = dto.IsCorrect ? 1 : 0,
                IsMastered = dto.IsCorrect,
                LastReviewedAt = DateTime.UtcNow
            };
            _context.UserProgresses.Add(progress);
        }
        else
        {
            if (dto.IsCorrect)
            {
                progress.CorrectStreak++;
                progress.IsMastered = true; // Mark as Mastered immediately on correct answer!
            }
            else
            {
                progress.CorrectStreak = 0;
                progress.IsMastered = false;
            }
            progress.LastReviewedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(new { QuestionId = id, progress.IsMastered, progress.CorrectStreak });
    }
}
