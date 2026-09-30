using System;

/// <summary>
/// Exceptions used
/// Author: math
/// </summary>

namespace b2xtranslator.StructuredStorage.Common
{
    public class MagicNumberException : Exception
    {
        public MagicNumberException()
            : base("Magic Number not found in file.")
        {
        }

        public MagicNumberException(string additionalMessage)
            : base("Magic Number not found in file. " + additionalMessage)
        {
        }
    }

    public class ValueNotZeroException : Exception
    {
        public ValueNotZeroException(string value)
            : base(value + " must be zero.")
        {
        }
    }

    public class ReadBytesAmountMismatchException : Exception
    {
        public ReadBytesAmountMismatchException()
            : base("The number of bytes read mismatches the specified amount.")
        {
        }
    }

    public class UnsupportedSizeException : Exception
    {
        public UnsupportedSizeException(string value)
            : base("The size of " + value + " is not supported.")
        {
        }
    }

    public class InvalidValueInHeaderException : Exception
    {
        public InvalidValueInHeaderException(string value)
            : base("The value for '" + value + "' in the header is invalid.")
        {
        }

        public InvalidValueInHeaderException(string value, string additionalMessage)
            : base("The value for '" + value + "' in the header is invalid. " + additionalMessage)
        {
        }
    }

    public class ChainCycleDetectedException : Exception
    {
        public ChainCycleDetectedException(string chain)
            : base(chain + " contains a cycle.")
        {
        }
    }

    public class ChainSizeMismatchException : Exception
    {
        public ChainSizeMismatchException(string name)
            : base("The number of sectors used by " + name + " does not match the specified size.")
        {
        }

        public ChainSizeMismatchException(string name, string additionalMessage)
            : base("The number of sectors used by " + name + " does not match the specified size. " + additionalMessage)
        {
        }
    }

    public class InvalidSectorInChainException : Exception
    {
        public InvalidSectorInChainException()
            : base("Chain could not be build due to an invalid sector id.")
        {
        }
    }

    public class StreamNotInitalizedException : Exception
    {
        public StreamNotInitalizedException()
            : base("The current stream is not initialized.")
        {
        }
    }

    public class InvalidValueInDirectoryEntryException : Exception
    {
        public InvalidValueInDirectoryEntryException(string value)
            : base("The value for '" + value + "' is invalid.")
        {
        }
    }

    public class WrongDirectoryEntryTypeException : Exception
    {
        public WrongDirectoryEntryTypeException()
            : base("The directory entry is not of type STGTY_STREAM.")
        {
        }
    }

    public class StreamNotFoundException : Exception
    {
        public StreamNotFoundException(string name)
            : base("Stream with name '" + name + "' not found.")
        {
        }
    }

    public class DirectoryEntryNotFoundException : Exception
    {
        public DirectoryEntryNotFoundException(string name)
            : base("DirectoryEntry with name '" + name + "' not found.")
        {
        }
    }

    public class FileHandlerNotCorrectlyInitializedException : Exception
    {
        public FileHandlerNotCorrectlyInitializedException()
            : base("The file handler is not correctly initialized.")
        {
        }
    }

    public class DiFatInconsistentException : Exception
    {
        public DiFatInconsistentException()
            : base("Inconsistancy found while writing DiFat.")
        {
        }
    }

    public class InvalidSectorSizeException : Exception
    {
        public InvalidSectorSizeException()
            : base("Inconsistancy found while writing a sector.")
        {
        }
    }

    /// <summary>
    /// Classifies runtime faults that record parsers raise when reading malformed or truncated data,
    /// so document parsers can rethrow them as their format's defined exception.
    /// </summary>
    public static class MalformedInput
    {
        public static bool IsParseFault(Exception ex) =>
            ex is System.IO.EndOfStreamException
            || ex is OutOfMemoryException          // size fields driving huge allocations
            || ex is InvalidOperationException
            || ex is ArgumentException
            || ex is IndexOutOfRangeException
            || ex is InvalidCastException
            || ex is OverflowException
            || ex is NullReferenceException
            || ex is System.Collections.Generic.KeyNotFoundException
            || ex is System.Reflection.TargetInvocationException
            || ex is FormatException
            || ex is DivideByZeroException;
    }

}
