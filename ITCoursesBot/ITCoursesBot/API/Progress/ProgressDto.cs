

public sealed record AddAnswerDto(
    long TelegramId,
    int QuestionId,
    string AnswerText,
    bool IsCorrect
);

public sealed record BlockStatsDto(
    string Course,
    int Block,
    int CorrectCount,
    int IncorrectCount
);


public sealed record CourseStatsDto(
    string Course,
    int CorrectCount,
    int IncorrectCount
);

public sealed record QuestionStatsDto(
    string Course,
    int? Block,
    int CorrectCount,
    int IncorrectCount,
    int UnansweredCount
);

public sealed record CourseProgressDto(
    string Course,
    int CorrectCount,
    int IncorrectCount,
    int UnansweredCount
);

public sealed record LessonErrorsDto(
    string LessonName,
    int WrongCount
);