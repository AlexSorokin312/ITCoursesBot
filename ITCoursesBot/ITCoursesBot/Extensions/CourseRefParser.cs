using System.Text.RegularExpressions;

public readonly record struct CourseRef(string Course, int Block, int Lesson);

public static class CourseRefParser
{
    // Блок и урок могут быть любой длины (1‑n цифр)
    private static readonly Regex _rx =
        new(@"^(?<course>\D+?)(?<block>\d+)(?<lesson>\d)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static CourseRef Parse(string input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        var m = _rx.Match(input);
        if (!m.Success)
            throw new FormatException(
                $"Строка «{input}» не соответствует формату <Курс><Block><Lesson> (пример: База21)");

        var course = m.Groups["course"].Value;              // «База»
        var block = int.Parse(m.Groups["block"].Value);    // 2
        var lesson = int.Parse(m.Groups["lesson"].Value);   // 1

        return new CourseRef(course, block, lesson);
    }

    public static bool TryParse(string input, out CourseRef result)
    {
        try
        {
            result = Parse(input);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }
}
