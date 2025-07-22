public interface IQuestionsClient
{
    Task<IReadOnlyList<Question>> GetLessonQuestionsAsync(string course, int block, int lessonNumber);
    Task<IReadOnlyList<Question>> GetBlockQuestionsAsync(string course, int block);
    Task<Question> AddQuestionAsync(AddQuestionDto dto);
    Task<IReadOnlyList<Question>> AddQuestionsAsync(AddQuestionsDto dto);
}