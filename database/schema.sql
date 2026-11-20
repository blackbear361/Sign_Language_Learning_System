CREATE TABLE IF NOT EXISTS users (
    Id INT NOT NULL AUTO_INCREMENT,
    FullName VARCHAR(120) NOT NULL,
    Email VARCHAR(254) NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,
    Role VARCHAR(30) NOT NULL DEFAULT 'User',
    Status VARCHAR(10) NOT NULL DEFAULT 'Active',
    RegistrationDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (Id),
    UNIQUE KEY UX_users_Email (Email)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- For existing installations, run once:
-- ALTER TABLE users ADD COLUMN Status VARCHAR(10) NOT NULL DEFAULT 'Active';

-- Promote an existing account to administrator after signup:
-- UPDATE users SET Role = 'Administrator' WHERE Email = 'admin@example.com';

CREATE TABLE IF NOT EXISTS user_profiles (
    UserId INT NOT NULL,
    Phone VARCHAR(40) NULL,
    Bio VARCHAR(2000) NULL,
    DateOfBirth DATE NULL,
    SkillLevel VARCHAR(30) NOT NULL DEFAULT 'Beginner',
    PRIMARY KEY (UserId),
    CONSTRAINT FK_user_profiles_users FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS lessons (
    Id INT NOT NULL AUTO_INCREMENT,
    Title VARCHAR(120) NOT NULL,
    Category VARCHAR(80) NOT NULL,
    Description VARCHAR(4000) NOT NULL,
    VideoUrl VARCHAR(500) NULL,
    IsPublished BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (Id),
    KEY IX_lessons_PublishedCategory (IsPublished, Category)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS lesson_progress (
    Id INT NOT NULL AUTO_INCREMENT,
    UserId INT NOT NULL,
    LessonId INT NOT NULL,
    CompletedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (Id),
    UNIQUE KEY UX_lesson_progress_UserLesson (UserId, LessonId),
    CONSTRAINT FK_lesson_progress_users FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE,
    CONSTRAINT FK_lesson_progress_lessons FOREIGN KEY (LessonId) REFERENCES lessons (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS quiz_questions (
    Id INT NOT NULL AUTO_INCREMENT,
    LessonId INT NOT NULL,
    QuestionText VARCHAR(300) NOT NULL,
    ImageUrl VARCHAR(500) NULL,
    ImageCredit VARCHAR(200) NULL,
    OptionA VARCHAR(120) NOT NULL,
    OptionB VARCHAR(120) NOT NULL,
    OptionC VARCHAR(120) NOT NULL,
    OptionD VARCHAR(120) NOT NULL,
    CorrectOption CHAR(1) NOT NULL,
    VideoUrl VARCHAR(500) NULL,
    RequiresVideo BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (Id),
    KEY IX_quiz_questions_Lesson (LessonId),
    CONSTRAINT FK_quiz_questions_lessons FOREIGN KEY (LessonId) REFERENCES lessons (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS quiz_results (
    Id INT NOT NULL AUTO_INCREMENT,
    UserId INT NOT NULL,
    Category VARCHAR(80) NOT NULL,
    Score INT NOT NULL,
    TotalQuestions INT NOT NULL,
    AnswerSummary LONGTEXT NOT NULL,
    AttemptedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (Id),
    KEY IX_quiz_results_UserDate (UserId, AttemptedAtUtc),
    CONSTRAINT FK_quiz_results_users FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
