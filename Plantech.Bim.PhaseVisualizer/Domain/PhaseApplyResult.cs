namespace Plantech.Bim.PhaseVisualizer.Domain;

internal enum PhaseApplyFailureReason
{
    None = 0,
    ModelUnavailable = 1,
    NoValidCriteria = 2,
    FilterPathUnavailable = 3,
    NoActiveOrVisibleView = 4,
    UnexpectedError = 5,
}

internal readonly struct PhaseApplyResult
{
    private PhaseApplyResult(bool isSuccess, PhaseApplyFailureReason failureReason, string? detail)
    {
        IsSuccess = isSuccess;
        FailureReason = failureReason;
        Detail = detail ?? string.Empty;
    }

    public bool IsSuccess { get; }
    public PhaseApplyFailureReason FailureReason { get; }
    public string Detail { get; }

    public static PhaseApplyResult Success(string? detail = null)
    {
        return new PhaseApplyResult(true, PhaseApplyFailureReason.None, detail);
    }

    public static PhaseApplyResult Failure(PhaseApplyFailureReason reason, string? detail = null)
    {
        return new PhaseApplyResult(false, reason, detail);
    }
}
