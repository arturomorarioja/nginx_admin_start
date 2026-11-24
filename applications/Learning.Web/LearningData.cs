using System.Text.Json;

namespace Learning.Web;

public record Course(int Id, string Slug, string Title, string Category, int Hours, string Summary, int Capacity);

public record Enrollment(
    Guid Id, int CourseId, string FullName, string Email,
    DateTimeOffset EnrolledAt, DateTimeOffset? CompletedAt, Guid? CertificateId);

public sealed record CapacityUpdate(int Capacity);

public sealed record CertificateIssue(Guid EnrollmentId, string StudentName, string CourseTitle, DateTimeOffset CompletedAt);

public sealed record CertificateResponse(Guid Id);

public sealed class LearningData
{
    private static readonly Course[] Catalogue =
    [
        new(101, "architecture", "Software Architecture Essentials", "Architecture", 12,
            "Choose structures and boundaries that fit actual system requirements.", 16),
        new(102, "quality", "Software Quality Engineering", "Testing", 16,
            "Design effective test strategies and turn quality risks into evidence.", 20),
        new(103, "integration", "System Integration", "Integration", 16,
            "Build reliable integrations and understand API and messaging trade-offs.", 18),
        new(104, "performance", "Web Performance", "Web engineering", 8,
            "Measure browser performance and improve the user experience.", 24)
    ];

    private sealed class StoredState
    {
        public List<Enrollment> Enrollments { get; set; } = [];
        public Dictionary<int, int> Capacities { get; set; } = [];
    }

    private readonly object _gate = new();
    private readonly string _file = "/var/lib/cedar/learning.json";
    private StoredState _state;

    public LearningData()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        _state = File.Exists(_file)
            ? JsonSerializer.Deserialize<StoredState>(File.ReadAllText(_file)) ?? new StoredState()
            : new StoredState();
    }

    public Course[] Courses()
    {
        lock (_gate)
        {
            return Catalogue.Select(course => course with
            {
                Capacity = _state.Capacities.GetValueOrDefault(course.Id, course.Capacity)
            }).ToArray();
        }
    }

    public Course? FindCourse(int id) => Courses().FirstOrDefault(course => course.Id == id);

    public Enrollment[] Enrollments()
    {
        lock (_gate)
        {
            return _state.Enrollments.ToArray();
        }
    }

    public Enrollment[] EnrollmentsFor(string email) => Enrollments()
        .Where(e => string.Equals(e.Email, email, StringComparison.OrdinalIgnoreCase)).ToArray();

    public Enrollment? GetEnrollment(Guid id) => Enrollments().FirstOrDefault(e => e.Id == id);

    public Enrollment Enroll(int courseId, string name, string email)
    {
        lock (_gate)
        {
            var course = Catalogue.FirstOrDefault(c => c.Id == courseId)
                ?? throw new ArgumentException("Course does not exist.");
            var existing = _state.Enrollments.FirstOrDefault(e => e.CourseId == courseId
                && string.Equals(e.Email, email, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) return existing;

            var capacity = _state.Capacities.GetValueOrDefault(courseId, course.Capacity);
            if (_state.Enrollments.Count(e => e.CourseId == courseId) >= capacity)
                throw new InvalidOperationException("This course is fully booked.");

            var enrolment = new Enrollment(Guid.NewGuid(), courseId, name, email,
                DateTimeOffset.UtcNow, null, null);
            _state.Enrollments.Add(enrolment);
            Save();
            return enrolment;
        }
    }

    public bool Complete(Guid enrolmentId, Guid certificateId)
    {
        lock (_gate)
        {
            var index = _state.Enrollments.FindIndex(e => e.Id == enrolmentId);
            if (index < 0) return false;
            var previous = _state.Enrollments[index];
            if (previous.CompletedAt is not null) return true;
            _state.Enrollments[index] = previous with
            {
                CompletedAt = DateTimeOffset.UtcNow,
                CertificateId = certificateId
            };
            Save();
            return true;
        }
    }

    public void SetCapacity(int courseId, int capacity)
    {
        if (capacity < 1 || capacity > 200) throw new ArgumentException("Capacity must be between 1 and 200.");
        lock (_gate)
        {
            if (!Catalogue.Any(c => c.Id == courseId)) throw new ArgumentException("Unknown course.");
            if (capacity < _state.Enrollments.Count(e => e.CourseId == courseId))
                throw new InvalidOperationException("Capacity cannot be lower than current enrolments.");
            _state.Capacities[courseId] = capacity;
            Save();
        }
    }

    private void Save()
    {
        var temporaryFile = _file + ".tmp";
        File.WriteAllText(temporaryFile, JsonSerializer.Serialize(_state));
        File.Move(temporaryFile, _file, true);
    }
}
