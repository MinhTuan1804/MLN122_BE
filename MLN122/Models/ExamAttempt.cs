namespace MLN122.Models;

public class ExamAttempt
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    
    public int TotalQuestions { get; set; } = 60;
    public int CorrectCount { get; set; }
    public double Score { get; set; } // Thang điểm 10
    public bool IsPassed { get; set; }
    public int TimeSpentSeconds { get; set; }

    public User? User { get; set; }
    public List<ExamDetail> Details { get; set; } = new();
}

public class ExamDetail
{
    public int Id { get; set; }
    public int ExamAttemptId { get; set; }
    public int QuestionId { get; set; }
    
    public string UserAnswer { get; set; } = string.Empty; // e.g., "A"
    public string CorrectAnswer { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public bool IsFlagged { get; set; }

    public ExamAttempt? ExamAttempt { get; set; }
    public Question? Question { get; set; }
}
