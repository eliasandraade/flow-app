namespace Flow.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entity, object key)
        : base($"{entity} '{key}' was not found.") { }
}
