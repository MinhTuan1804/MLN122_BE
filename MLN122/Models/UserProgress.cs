namespace MLN122.Models;

public class UserProgress
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int QuestionId { get; set; }
    
    public bool IsStarred { get; set; } = false;
    public bool IsMastered { get; set; } = false;
    public int CorrectStreak { get; set; } = 0;
    public DateTime LastReviewedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Question? Question { get; set; }
}
