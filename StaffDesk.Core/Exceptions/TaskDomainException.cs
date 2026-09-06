namespace StaffDesk.Core.Exceptions;

// Thrown by TaskService for domain-rule violations that need a specific
// error code + HTTP status in the API response envelope (see ApiError in StaffDesk.API).
public class TaskDomainException : Exception
{
    public string Code { get; }
    public int HttpStatus { get; }
    public List<string> Details { get; }

    public TaskDomainException(string code, string message, int httpStatus, List<string>? details = null)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
        Details = details ?? new List<string>();
    }
}

public static class TaskErrorCodes
{
    public const string InvalidTransition = "TASK_INVALID_TRANSITION";           // 409
    public const string AssignmentNotPermitted = "TASK_ASSIGNMENT_NOT_PERMITTED"; // 403
    public const string EmployeeInactive = "EMPLOYEE_INACTIVE";                  // 422
    public const string ReasonRequired = "REASON_REQUIRED";                      // 422
    public const string HasOpenBlockers = "TASK_HAS_OPEN_BLOCKERS";              // 422 (also used for open checklist items)
    public const string CircularDependency = "TASK_CIRCULAR_DEPENDENCY";         // 422
    public const string NestingTooDeep = "TASK_NESTING_TOO_DEEP";                // 422
    public const string CrossDepartment = "CROSS_DEPARTMENT_ASSIGNMENT";         // 422
    public const string ValidationError = "VALIDATION_ERROR";                    // 400
    public const string NotFound = "NOT_FOUND";                                  // 404
    public const string LimitExceeded = "LIMIT_EXCEEDED";                        // 422
    public const string TagCreationNotPermitted = "TAG_CREATION_NOT_PERMITTED";  // 403
    public const string Forbidden = "FORBIDDEN";                                 // 403
    public const string NoEmployeeLink = "NO_EMPLOYEE_LINK";                     // 403
    public const string UnknownQueryParam = "UNKNOWN_QUERY_PARAM";               // 400
    public const string InternalError = "INTERNAL_ERROR";                        // 500

    // Part B - WC-3: task request lifecycle enforcement (SUBMITTED/UNDER_TRIAGE -> ACCEPTED|DECLINED|MERGED).
    public const string TaskRequestInvalidTransition = "TASK_REQUEST_INVALID_TRANSITION"; // 409

    // Part B - WC-13: transition to DONE refused while any acceptance criterion is unmet, unless
    // the department manager/Admin supplies an override reason.
    public const string AcceptanceCriteriaUnmet = "TASK_ACCEPTANCE_CRITERIA_UNMET"; // 422

    public const string DuplicateResource = "DUPLICATE_RESOURCE"; // 409
    public const string LegalHoldActive = "LEGAL_HOLD_ACTIVE"; // 422

    // Part F - PL-2/PL-5: idempotency key reused with a different body, or a stale If-Match on an
    // optimistic-concurrency-protected update.
    public const string IdempotencyKeyConflict = "IDEMPOTENCY_KEY_CONFLICT"; // 422
    public const string PreconditionFailed = "PRECONDITION_FAILED"; // 412
    public const string PreconditionRequired = "PRECONDITION_REQUIRED"; // 428
    public const string Unauthorized = "UNAUTHORIZED"; // 401
}
