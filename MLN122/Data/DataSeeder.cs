using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MLN122.Models;

namespace MLN122.Data;

public static class DataSeeder
{
    public class RawQuestionDto
    {
        public string num { get; set; } = string.Empty;
        public string content { get; set; } = string.Empty;
        public Dictionary<string, string> options { get; set; } = new();
        public string answer { get; set; } = string.Empty;
        public string explanation { get; set; } = string.Empty;
    }

    public static async Task SeedAsync(AppDbContext context, IWebHostEnvironment env)
    {
        if (await context.Questions.AnyAsync())
        {
            return; // Already seeded
        }

        var jsonPath = Path.Combine(env.ContentRootPath, "Data", "questions_seed.json");
        if (!File.Exists(jsonPath))
        {
            jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "questions_seed.json");
        }

        if (!File.Exists(jsonPath))
        {
            Console.WriteLine($"Seed JSON file not found at: {jsonPath}");
            return;
        }

        var jsonStr = await File.ReadAllTextAsync(jsonPath);
        var rawList = JsonSerializer.Deserialize<List<RawQuestionDto>>(jsonStr, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (rawList == null || rawList.Count == 0) return;

        var questions = new List<Question>();

        foreach (var item in rawList)
        {
            var q = new Question
            {
                QuestionNum = item.num,
                Content = item.content,
                CorrectAnswer = item.answer,
                Explanation = item.explanation,
                Options = item.options.Select(kvp => new Option
                {
                    Key = kvp.Key,
                    Content = kvp.Value
                }).ToList()
            };
            questions.Add(q);
        }

        await context.Questions.AddRangeAsync(questions);
        await context.SaveChangesAsync();
        Console.WriteLine($"Successfully seeded {questions.Count} questions into database.");
    }
}
