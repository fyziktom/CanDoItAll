namespace CanDoItAll.SharedKernel;

public class Result
{
    private readonly IReadOnlyList<Error> _errors;

    protected Result(bool isSuccess, IReadOnlyList<Error>? errors = null)
    {
        IsSuccess = isSuccess;
        _errors = errors ?? [];
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public IReadOnlyList<Error> Errors => _errors;

    public static Result Success() => new(true);

    public static Result Failure(params Error[] errors) => new(false, errors);

    public static Result Failure(IEnumerable<Error> errors) => new(false, errors.ToArray());
}

public sealed class Result<T> : Result
{
    private Result(T? value, bool isSuccess, IReadOnlyList<Error>? errors = null)
        : base(isSuccess, errors)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(value, true);

    public static new Result<T> Failure(params Error[] errors) => new(default, false, errors);

    public static new Result<T> Failure(IEnumerable<Error> errors) => new(default, false, errors.ToArray());
}
