using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MLN122.Models;

public class User
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;

    public int LastQuestionIndex { get; set; } = 0;
    public string LastStudyMode { get; set; } = "flashcard"; // "flashcard" | "practice"
    public string LastFilterType { get; set; } = "all"; // "all" | "starred" | "wrong" | "mastered"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<UserProgress> ProgressList { get; set; } = new();
    public List<ExamAttempt> ExamAttempts { get; set; } = new();
}
