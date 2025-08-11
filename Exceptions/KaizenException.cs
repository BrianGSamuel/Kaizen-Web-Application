namespace KaizenWebApp.Exceptions
{
    public class KaizenException : Exception
    {
        public KaizenException() : base() { }
        public KaizenException(string message) : base(message) { }
        public KaizenException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class KaizenNotFoundException : KaizenException
    {
        public KaizenNotFoundException(int id) : base($"Kaizen with ID {id} was not found.") { }
    }

    public class UserNotFoundException : KaizenException
    {
        public UserNotFoundException(string username) : base($"User '{username}' was not found.") { }
    }

    public class InvalidFileException : KaizenException
    {
        public InvalidFileException(string message) : base(message) { }
    }

    public class UnauthorizedAccessException : KaizenException
    {
        public UnauthorizedAccessException(string message) : base(message) { }
    }
}
