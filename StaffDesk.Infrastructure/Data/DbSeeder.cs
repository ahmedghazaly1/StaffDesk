using StaffDesk.Core.Entities;

namespace StaffDesk.Infrastructure.Data;

public static class DbSeeder
{
    // ============================================
    // Seniority Levels (OR-3)
    // ============================================
    public static void SeedSeniorityLevels(AppDbContext context)
    {
        if (context.SeniorityLevels.Any()) return;

        var levels = new[]
        {
            new SeniorityLevel { Name = "Intern", Rank = 10, Description = "Entry-level position", IsActive = true },
            new SeniorityLevel { Name = "Junior", Rank = 20, Description = "Junior level", IsActive = true },
            new SeniorityLevel { Name = "Mid", Rank = 30, Description = "Mid-level", IsActive = true },
            new SeniorityLevel { Name = "Senior", Rank = 40, Description = "Senior level", IsActive = true },
            new SeniorityLevel { Name = "Lead", Rank = 50, Description = "Team lead", IsActive = true },
            new SeniorityLevel { Name = "Manager", Rank = 60, Description = "Manager", IsActive = true },
            new SeniorityLevel { Name = "Director", Rank = 70, Description = "Director", IsActive = true }
        };

        context.SeniorityLevels.AddRange(levels);
        context.SaveChanges();
    }

    // ============================================
    // Departments & Employees - Complete hierarchy
    // ============================================
    public static void Seed(AppDbContext context)
    {
        SeedSeniorityLevels(context);

        if (context.Employees.Any()) return;

        var departments = new[]
        {
            new Department { Name = "Engineering", Location = "Cairo Office" },
            new Department { Name = "Marketing", Location = "Dubai Office" },
            new Department { Name = "Sales", Location = "Riyadh Office" },
            new Department { Name = "Human Resources", Location = "Cairo Office" },
            new Department { Name = "Finance", Location = "Dubai Office" },
            new Department { Name = "Operations", Location = "Cairo Office" },
            new Department { Name = "IT Support", Location = "Cairo Office" },
            new Department { Name = "Legal", Location = "Dubai Office" }
        };

        if (!context.Departments.Any())
        {
            context.Departments.AddRange(departments);
            context.SaveChanges();
        }

        var deptByName = context.Departments.ToDictionary(d => d.Name, d => d);
        var levelByName = context.SeniorityLevels.ToDictionary(l => l.Name, l => l);

        // 30+ employees across all departments with proper hierarchy
        var roster = new (string FullName, string JobTitle, string Dept, string Level, string? Manager, bool IsDeptManager)[]
        {
            // Operations - Top of hierarchy
            ("David Wilson", "Director of Operations", "Operations", "Director", null, true),
            
            // Engineering
            ("Michael Chen", "Engineering Manager", "Engineering", "Manager", "David Wilson", true),
            ("Sarah Johnson", "Senior Software Engineer", "Engineering", "Senior", "Michael Chen", false),
            ("Ahmed Ghazaly", "Software Engineer", "Engineering", "Mid", "Michael Chen", false),
            ("Omar Hassan", "Junior Software Engineer", "Engineering", "Junior", "Sarah Johnson", false),
            ("Nora Ibrahim", "Engineering Intern", "Engineering", "Intern", "Sarah Johnson", false),
            ("Youssef Ali", "DevOps Engineer", "Engineering", "Mid", "Michael Chen", false),
            ("Layla Hassan", "QA Engineer", "Engineering", "Junior", "Sarah Johnson", false),
            
            // Marketing
            ("Emily Davis", "Marketing Manager", "Marketing", "Manager", "David Wilson", true),
            ("Matthew Taylor", "Marketing Analyst", "Marketing", "Junior", "Emily Davis", false),
            ("Rachel Green", "Content Strategist", "Marketing", "Mid", "Emily Davis", false),
            ("Amy Santiago", "Social Media Manager", "Marketing", "Senior", "Emily Davis", false),
            
            // Sales
            ("Michael Brown", "Sales Team Lead", "Sales", "Lead", "David Wilson", true),
            ("Jessica Lee", "Sales Associate", "Sales", "Junior", "Michael Brown", false),
            ("James Rodriguez", "Senior Sales Executive", "Sales", "Senior", "Michael Brown", false),
            ("Linda Park", "Account Manager", "Sales", "Mid", "Michael Brown", false),
            
            // Human Resources
            ("Joshua Garcia", "HR Manager", "Human Resources", "Manager", "David Wilson", true),
            ("Patricia Moore", "HR Specialist", "Human Resources", "Mid", "Joshua Garcia", false),
            ("Karen White", "Recruitment Lead", "Human Resources", "Senior", "Joshua Garcia", false),
            
            // Finance
            ("Daniel Clark", "Finance Manager", "Finance", "Manager", "David Wilson", true),
            ("Laura Robinson", "Internal Auditor", "Finance", "Senior", "Daniel Clark", false),
            ("Kevin Martinez", "Finance Analyst", "Finance", "Mid", "Daniel Clark", false),
            
            // IT Support
            ("Thomas Anderson", "IT Support Manager", "IT Support", "Manager", "David Wilson", true),
            ("Maria Smith", "IT Support Specialist", "IT Support", "Mid", "Thomas Anderson", false),
            
            // Legal
            ("Jennifer Williams", "Legal Counsel", "Legal", "Director", "David Wilson", true),
            ("Robert Miller", "Contracts Manager", "Legal", "Senior", "Jennifer Williams", false)
        };

        var employees = roster.Select(r => new Employee
        {
            FullName = r.FullName,
            JobTitle = r.JobTitle,
            DepartmentId = deptByName[r.Dept].Id,
            LevelId = levelByName[r.Level].Id,
            IsActive = true
        }).ToList();

        context.Employees.AddRange(employees);
        context.SaveChanges();

        var employeeByName = employees.ToDictionary(e => e.FullName, e => e);

        foreach (var r in roster)
        {
            if (r.Manager != null)
                employeeByName[r.FullName].ManagerId = employeeByName[r.Manager].Id;

            if (r.IsDeptManager)
                deptByName[r.Dept].ManagerId = employeeByName[r.FullName].Id;
        }

        context.SaveChanges();
    }

    // ============================================
    // Users - Complete role set
    // ============================================
    public static void SeedUsers(AppDbContext context)
    {
        var employeeByName = context.Employees.ToDictionary(e => e.FullName, e => e.Id);

        var accounts = new (string Username, string Password, string Email, string Role, string EmployeeName)[]
        {
            ("admin", "admin123", "admin@staffdesk.com", User.Roles.Admin, "David Wilson"),
            ("manager", "manager123", "manager@staffdesk.com", User.Roles.Manager, "Michael Chen"),
            ("member", "member123", "member@staffdesk.com", User.Roles.Member, "Ahmed Ghazaly"),
            ("member2", "member123", "member2@staffdesk.com", User.Roles.Member, "Omar Hassan"),
            ("auditor", "auditor123", "auditor@staffdesk.com", User.Roles.Auditor, "Laura Robinson"),
            ("hr", "hr123", "hr@staffdesk.com", User.Roles.HrAdmin, "Patricia Moore"),
            ("deptmanager", "dept123", "dept.manager@staffdesk.com", User.Roles.Manager, "Emily Davis"),
            ("lead", "lead123", "lead@staffdesk.com", User.Roles.Member, "Michael Brown"),
            ("intern", "intern123", "intern@staffdesk.com", User.Roles.Member, "Nora Ibrahim")
        };

        var existing = context.Users.Select(u => u.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toAdd = accounts
            .Where(a => !existing.Contains(a.Username))
            .Select(a => new User
            {
                Username = a.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(a.Password),
                Email = a.Email,
                Role = a.Role,
                EmployeeId = employeeByName.GetValueOrDefault(a.EmployeeName),
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        if (toAdd.Count == 0) return;

        context.Users.AddRange(toAdd);
        context.SaveChanges();
    }

    // ============================================
    // Tasks - 30+ tasks across all statuses, priorities, and departments
    // ============================================
    public static void SeedTasks(AppDbContext context)
    {
        if (context.Tasks.Any()) return;

        var e = context.Employees.ToDictionary(x => x.FullName, x => x);
        var d = context.Departments.ToDictionary(x => x.Name, x => x);
        var now = DateTime.UtcNow;

        var seedTasks = new (string Title, string Description, string Dept, string CreatedBy, string? Assignee, string Status, string Priority, int? DueInDays)[]
        {
            // Engineering Tasks (Department: Engineering)
            ("Set up CI pipeline", "Configure build and test automation for the API project.", "Engineering", "Michael Chen", "Ahmed Ghazaly", "OPEN", "HIGH", 5),
            ("Fix login token expiry bug", "Users are logged out earlier than the configured expiry.", "Engineering", "Sarah Johnson", "Sarah Johnson", "IN_PROGRESS", "URGENT", -1),
            ("Refactor task repository queries", "Split the large query methods into smaller, testable pieces.", "Engineering", "Michael Chen", "Omar Hassan", "OPEN", "NORMAL", 14),
            ("Onboard new intern", "Prepare accounts, equipment, and a ramp-up plan.", "Engineering", "Sarah Johnson", "Nora Ibrahim", "IN_REVIEW", "NORMAL", null),
            ("Investigate flaky integration test", "Test suite intermittently fails on the assignment endpoint.", "Engineering", "Ahmed Ghazaly", null, "BLOCKED", "HIGH", 3),
            ("Implement audit logging", "Add comprehensive audit trail for all security events.", "Engineering", "Michael Chen", "Youssef Ali", "IN_PROGRESS", "HIGH", 7),
            ("Update API documentation", "Refresh OpenAPI spec with all Phase 3 endpoints.", "Engineering", "Sarah Johnson", "Layla Hassan", "OPEN", "NORMAL", 10),
            ("Fix rework tracking bug", "Rework counts are not incrementing correctly", "Engineering", "Ahmed Ghazaly", "Ahmed Ghazaly", "IN_PROGRESS", "HIGH", 2),
            ("Optimize task list query", "Task list endpoint is slow with many tasks", "Engineering", "Michael Chen", "Omar Hassan", "OPEN", "NORMAL", 15),
            ("Deploy to staging", "Deploy latest changes to staging environment", "Engineering", "Youssef Ali", null, "BLOCKED", "URGENT", 1),

            // Marketing Tasks (Department: Marketing)
            ("Q3 social media calendar", "Plan and schedule posts for the next quarter.", "Marketing", "Emily Davis", "Matthew Taylor", "OPEN", "NORMAL", 10),
            ("Rebrand landing page copy", "Update messaging to match the new brand guidelines.", "Marketing", "Emily Davis", "Emily Davis", "DONE", "LOW", -10),
            ("Marketing campaign assets", "Create visuals for the new product launch", "Marketing", "Rachel Green", "Rachel Green", "IN_PROGRESS", "HIGH", 4),
            ("Monthly newsletter", "Prepare and send September newsletter", "Marketing", "Amy Santiago", "Matthew Taylor", "OPEN", "NORMAL", 8),
            ("Competitor analysis report", "Analyze competitor marketing strategies", "Marketing", "Emily Davis", "Amy Santiago", "IN_REVIEW", "NORMAL", null),

            // Sales Tasks (Department: Sales)
            ("Prepare client renewal deck", "Slides for the upcoming Acme Corp renewal call.", "Sales", "Michael Brown", "Jessica Lee", "IN_PROGRESS", "URGENT", 1),
            ("Update CRM contact list", "Remove stale leads and tag active accounts.", "Sales", "Michael Brown", "Michael Brown", "CANCELLED", "LOW", null),
            ("Q4 sales forecast", "Prepare forecast for Q4 targets", "Sales", "James Rodriguez", "James Rodriguez", "OPEN", "HIGH", 12),
            ("Client meeting prep", "Prepare for the meeting with potential client", "Sales", "Michael Brown", "Linda Park", "OPEN", "NORMAL", 6),
            ("Update sales deck", "Refresh sales presentation with new metrics", "Sales", "Jessica Lee", "Jessica Lee", "IN_PROGRESS", "NORMAL", 9),

            // Human Resources Tasks (Department: Human Resources)
            ("Draft new PTO policy", "Align policy wording with updated labour regulations.", "Human Resources", "Joshua Garcia", "Patricia Moore", "OPEN", "NORMAL", 20),
            ("Employee satisfaction survey", "Prepare and distribute Q3 satisfaction survey.", "Human Resources", "Patricia Moore", "Karen White", "IN_PROGRESS", "HIGH", 5),
            ("Onboarding process review", "Review and improve employee onboarding", "Human Resources", "Joshua Garcia", "Patricia Moore", "IN_REVIEW", "NORMAL", null),
            ("Update employee handbook", "Review and update handbook policies", "Human Resources", "Karen White", "Karen White", "OPEN", "LOW", 30),

            // Finance Tasks (Department: Finance)
            ("Run quarterly payroll audit", "Verify payroll entries against timesheets for Q2.", "Finance", "Daniel Clark", "Laura Robinson", "IN_REVIEW", "HIGH", -2),
            ("Reconcile vendor invoices", "Match outstanding vendor invoices against purchase orders.", "Finance", "Daniel Clark", "Daniel Clark", "OPEN", "NORMAL", 7),
            ("Audit department access logs", "Cross-check task activity logs against the permission matrix.", "Finance", "Laura Robinson", "Laura Robinson", "IN_PROGRESS", "NORMAL", 4),
            ("Prepare monthly budget report", "Compile and analyse monthly spending", "Finance", "Daniel Clark", "Kevin Martinez", "OPEN", "HIGH", 3),
            ("Tax compliance review", "Review tax filings and compliance", "Finance", "Laura Robinson", "Daniel Clark", "DONE", "NORMAL", -5),

            // Operations Tasks (Department: Operations)
            ("Renew office lease", "Negotiate and sign the Cairo office lease renewal.", "Operations", "David Wilson", "Kevin Martinez", "OPEN", "HIGH", 30),
            ("Company all-hands planning", "Coordinate logistics for the upcoming all-hands meeting.", "Operations", "David Wilson", "David Wilson", "DONE", "NORMAL", -15),
            ("Office safety inspection", "Conduct safety inspection", "Operations", "David Wilson", "Kevin Martinez", "OPEN", "NORMAL", 14),
            ("Vendor contract review", "Review all vendor contracts", "Operations", "David Wilson", null, "IN_PROGRESS", "HIGH", 8),
            
            // IT Support Tasks (Department: IT Support)
            ("Upgrade company laptops", "Roll out new laptops to all employees", "IT Support", "Thomas Anderson", "Maria Smith", "IN_PROGRESS", "NORMAL", 14),
            ("Set up new office network", "Configure network for new office", "IT Support", "Thomas Anderson", null, "BLOCKED", "HIGH", 10),
            
            // Legal Tasks (Department: Legal)
            ("Review compliance policies", "Review all compliance policies for GDPR", "Legal", "Jennifer Williams", "Robert Miller", "OPEN", "URGENT", 7),
            ("Update terms of service", "Review and update terms", "Legal", "Robert Miller", "Robert Miller", "IN_REVIEW", "NORMAL", null)
        };

        var tasks = seedTasks.Select(t => new WorkTask
        {
            Key = $"SEED-{Guid.NewGuid():N}",
            Title = t.Title,
            Description = t.Description,
            DepartmentId = d[t.Dept].Id,
            CreatedById = e[t.CreatedBy].Id,
            AssigneeId = t.Assignee != null ? e[t.Assignee].Id : null,
            Status = t.Status,
            Priority = t.Priority,
            DueAt = t.DueInDays.HasValue ? now.AddDays(t.DueInDays.Value) : null,
            IsArchived = false,
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = t.Status == "DONE" ? now.AddDays(t.DueInDays ?? -1) : null
        }).ToList();

        context.Tasks.AddRange(tasks);
        context.SaveChanges();

        foreach (var task in tasks)
        {
            task.Key = $"TSK-{task.Id}";
        }
        context.SaveChanges();
    }

    // ============================================
    // Competency Library (PM-13/14)
    // ============================================
    public static void SeedCompetencies(AppDbContext context)
    {
        if (context.Competencies.Any()) return;
        SeedSeniorityLevels(context);

        var levels = context.SeniorityLevels.OrderBy(l => l.Rank).ToList();
        if (levels.Count == 0) return;

        var defs = new (string Category, string Name, string Description, int? MinRank)[]
        {
            ("Delivery", "Quality of work", "Delivers work that meets agreed standards.", null),
            ("Delivery", "Reliability and follow-through", "Completes commitments predictably.", null),
            ("Delivery", "Ownership of outcomes", "Owns results end to end.", null),
            ("Delivery", "Estimation and planning", "Plans and estimates work realistically.", null),
            ("Craft", "Technical depth", "Applies deep technical skill appropriately.", null),
            ("Craft", "Problem decomposition", "Breaks ambiguous problems into clear work.", null),
            ("Craft", "Code quality and review", "Writes and reviews maintainable code.", null),
            ("Craft", "Documentation", "Documents decisions and systems clearly.", null),
            ("Collaboration", "Communication", "Communicates clearly with stakeholders.", null),
            ("Collaboration", "Feedback given and received", "Gives and receives feedback constructively.", null),
            ("Collaboration", "Cross-team working", "Works effectively across team boundaries.", null),
            ("Collaboration", "Knowledge sharing", "Shares knowledge that helps others succeed.", null),
            ("Growth", "Learning and adaptability", "Learns quickly and adapts to change.", null),
            ("Growth", "Initiative", "Acts without waiting for perfect instructions.", null),
            ("Growth", "Mentoring others", "Develops others through mentoring.", 40),
            ("Judgement", "Prioritisation", "Focuses effort on the highest-value work.", null),
            ("Judgement", "Risk awareness", "Surfaces and manages risk early.", null),
            ("Judgement", "Decision quality under uncertainty", "Decides well with incomplete information.", null)
        };

        foreach (var d in defs)
        {
            var competency = new Competency
            {
                Name = d.Name,
                Description = d.Description,
                Category = d.Category,
                MinSeniorityRank = d.MinRank,
                IsActive = true
            };

            foreach (var level in levels)
            {
                if (d.MinRank != null && level.Rank < d.MinRank) continue;
                competency.LevelDescriptors.Add(new CompetencyLevelDescriptor
                {
                    SeniorityLevelId = level.Id,
                    Descriptor = $"At {level.Name}: demonstrates {d.Name.ToLowerInvariant()} consistent with expectations for this level."
                });
            }

            context.Competencies.Add(competency);
        }

        context.SaveChanges();
    }

    // ============================================
    // SLA Policies (SL-1)
    // ============================================
    public static void SeedSlaPolicies(AppDbContext context)
    {
        if (context.SlaPolicies.Any()) return;

        var engineering = context.Departments.FirstOrDefault(d => d.Name == "Engineering");
        var sales = context.Departments.FirstOrDefault(d => d.Name == "Sales");
        var marketing = context.Departments.FirstOrDefault(d => d.Name == "Marketing");

        if (engineering == null) return;

        var policies = new List<SlaPolicy>();

        // Engineering
        policies.Add(new SlaPolicy
        {
            DepartmentId = engineering.Id,
            Priority = "URGENT",
            ResponseTargetMinutes = 60,
            ResolutionTargetMinutes = 240,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        policies.Add(new SlaPolicy
        {
            DepartmentId = engineering.Id,
            Priority = "HIGH",
            ResponseTargetMinutes = 120,
            ResolutionTargetMinutes = 480,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        policies.Add(new SlaPolicy
        {
            DepartmentId = engineering.Id,
            Priority = "NORMAL",
            ResponseTargetMinutes = 240,
            ResolutionTargetMinutes = 960,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        if (sales != null)
        {
            policies.Add(new SlaPolicy
            {
                DepartmentId = sales.Id,
                Priority = "URGENT",
                ResponseTargetMinutes = 30,
                ResolutionTargetMinutes = 120,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (marketing != null)
        {
            policies.Add(new SlaPolicy
            {
                DepartmentId = marketing.Id,
                Priority = "NORMAL",
                ResponseTargetMinutes = 180,
                ResolutionTargetMinutes = 720,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        context.SlaPolicies.AddRange(policies);
        context.SaveChanges();
    }

    // ============================================
    // Master Seed - Call this from Program.cs
    // ============================================
    public static void SeedAll(AppDbContext context)
    {
        // Phase 1 & 2
        SeedSeniorityLevels(context);
        Seed(context);
        SeedUsers(context);
        SeedTasks(context);

        // Phase 3
        SeedCompetencies(context);
        SeedSlaPolicies(context);

        // Note: Rework events, status intervals, and other dynamic data
        // are created by the application during normal operation, not seeded.
    }
}