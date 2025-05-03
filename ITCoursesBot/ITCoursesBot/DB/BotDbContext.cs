using Microsoft.EntityFrameworkCore;

namespace ITCoursesBot.DB
{
    public class BotDbContext : DbContext
    {
        public BotDbContext(DbContextOptions<BotDbContext> options)
            : base(options)
        {
        }
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Course> Courses { get; set; } = null!;
        public DbSet<Lesson> Lessons { get; set; } = null!;
        public DbSet<Question> Questions { get; set; } = null!;
        public DbSet<UserAnswer> UserAnswers { get; set; } = null!;
        public DbSet<AIUserRequests> AIUserRequests { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder model)
        {
            base.OnModelCreating(model);

            // --- User ---
            model.Entity<User>(b =>
            {
                b.HasKey(u => u.Id);
                b.HasIndex(u => u.Username);                     // можно IsUnique(true), если нужны уникальные ники
                b.Property(u => u.FirstLaunch).IsRequired();
                b.Property(u => u.LastUse).IsRequired();
                b.Property(u => u.UsageCount).IsRequired();

                b.HasMany(u => u.Answers)
                 .WithOne(a => a.User)
                 .HasForeignKey(a => a.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                b.HasMany(u => u.AIRequests)
                 .WithOne(r => r.User)
                 .HasForeignKey(r => r.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // --- Course ---
            model.Entity<Course>(b =>
            {
                b.HasKey(c => c.Id);
                b.HasIndex(c => c.Name).IsUnique();
                b.HasIndex(c => c.ShortName).IsUnique();

                b.HasMany(c => c.Lessons)
                 .WithOne(l => l.Course)
                 .HasForeignKey(l => l.CourseId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // --- Lesson ---
            model.Entity<Lesson>(b =>
            {
                b.HasKey(l => l.Id);
                b.Property(l => l.Title).IsRequired();

                b.HasOne(l => l.Course)
                 .WithMany(c => c.Lessons)
                 .HasForeignKey(l => l.CourseId);

                b.HasMany(l => l.Questions)
                 .WithOne(q => q.Lesson)
                 .HasForeignKey(q => q.LessonId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // --- Question ---
            model.Entity<Question>(b =>
            {
                b.HasKey(q => q.Id);
                b.Property(q => q.Text).IsRequired();

                b.HasOne(q => q.Lesson)
                 .WithMany(l => l.Questions)
                 .HasForeignKey(q => q.LessonId);

                b.HasMany(q => q.UserAnswers)
                 .WithOne(a => a.Question)
                 .HasForeignKey(a => a.QuestionId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // --- UserAnswer ---
            model.Entity<UserAnswer>(b =>
            {
                b.HasKey(a => a.Id);
                b.Property(a => a.AnswerText).IsRequired();
                b.Property(a => a.IsCorrect).IsRequired();
                b.Property(a => a.AnsweredAt).IsRequired();

                b.HasOne(a => a.User)
                 .WithMany(u => u.Answers)
                 .HasForeignKey(a => a.UserId);

                b.HasOne(a => a.Question)
                 .WithMany(q => q.UserAnswers)
                 .HasForeignKey(a => a.QuestionId);
            });

            // --- AIUserRequests ---
            model.Entity<AIUserRequests>(b =>
            {
                b.HasKey(r => r.Id);
                b.Property(r => r.Date).IsRequired();
                b.Property(r => r.Count).IsRequired();

                b.HasIndex(r => new { r.UserId, r.Date })
                 .IsUnique();

                b.HasOne(r => r.User)
                 .WithMany(u => u.AIRequests)
                 .HasForeignKey(r => r.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
