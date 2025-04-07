namespace Helios.HTTP.Utilities.Classes;

public class ParserOptions
{
    /// <summary>
    /// Maximum buffer size for reading request body
    /// </summary>
    public int MaxBufferSize { get; set; } = 1024 * 1024; 
        
    /// <summary>
    /// Whether to use pooled memory for buffer allocation
    /// </summary>
    public bool UsePooledMemory { get; set; } = true;
        
    /// <summary>
    /// Whether to throw exceptions when parsing errors occur
    /// </summary>
    public bool ThrowOnError { get; set; } = false;
}