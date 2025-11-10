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

    public readonly record struct LessonRef(string Course, int Block, int Lesson);

    // Правило:
    // - Если цифр ровно 2: блок = первая цифра, урок = вторая.
    // - Если цифр >= 3: урок = последние 2 цифры, блок = всё, что перед ними.
    public static bool TryParse(string input, out LessonRef lessonRef)
    {
        lessonRef = default;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var raw = input.Trim();

        // 1) Пытаемся распарсить формат с пробелом:
        //    "<Курс><Блок> <Урок>" или "<Курс> <Блок> <Урок>"
        //    Пример: "База2 1", "Продв3 20", "QA 12 10"
        var spaced = Regex.Match(
            raw,
            @"^(?<course>[^\d]+?)\s*(?<block>\d+)\s+(?<lesson>\d+)$",
            RegexOptions.CultureInvariant);

        if (spaced.Success)
        {
            var course = spaced.Groups["course"].Value.Trim();
            if (!int.TryParse(spaced.Groups["block"].Value, out var block)) return false;
            if (!int.TryParse(spaced.Groups["lesson"].Value, out var lesson)) return false;

            if (block <= 0 || lesson <= 0) return false;

            lessonRef = new LessonRef(course, block, lesson);
            return true;
        }

        // 2) Фолбэк: компактный формат без пробелов
        //    "<Курс><Цифры>"
        //    Правило:
        //      - если цифр ровно 2: блок = первая, урок = вторая
        //      - если цифр >= 3: урок = последние 2 цифры, блок = всё, что перед ними
        var cleaned = raw.Replace(" ", "");
        var compact = Regex.Match(
            cleaned,
            @"^(?<course>\D+?)(?<nums>\d+)$",
            RegexOptions.CultureInvariant);

        if (!compact.Success)
            return false;

        var courseCompact = compact.Groups["course"].Value;
        var nums = compact.Groups["nums"].Value;

        if (nums.Length < 2) return false;

        int blockCompact, lessonCompact;

        if (nums.Length == 2)
        {
            blockCompact = nums[0] - '0';
            lessonCompact = nums[1] - '0';
        }
        else
        {
            lessonCompact = int.Parse(nums[^2..]);
            blockCompact = int.Parse(nums[..^2]);
        }

        if (blockCompact <= 0 || lessonCompact <= 0)
            return false;

        lessonRef = new LessonRef(courseCompact, blockCompact, lessonCompact);
        return true;
    }
}
