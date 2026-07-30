namespace MLN122.Models;

public class Question
{
    public int Id { get; set; }
    public string QuestionNum { get; set; } = string.Empty; // e.g. "Câu 1"
    public string Content { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty; // e.g. "A", "A,B,C"
    public string Explanation { get; set; } = string.Empty;

    public List<Option> Options { get; set; } = new();
}

public class Option
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string Key { get; set; } = string.Empty; // "A", "B", "C", "D", "E"
    public string Content { get; set; } = string.Empty;
}
