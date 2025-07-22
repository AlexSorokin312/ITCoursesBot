public sealed record NewLessonDto(
    string Course,
    int Block,
    int Number,
    string Title
);

public sealed record NewLessonEndDto(
    string Course,
    int Block,
    string Title
);

public sealed record LessonDto(
    int Id,
    string Course,
    int Block,
    int Number,
    string Title,
    DateTime CreatedAt  // если в вашей сущности есть время; иначе уберите
);