using System.Security.Cryptography;
using System.Text;

namespace Helios.Utilities;

public class HashAlgorithmGenerator
{
    /// <summary>
    /// Generates a hash from the provided string using the specified hash algorithm.
    /// </summary>
    /// <param name="content">The string to be hashed.</param>
    /// <param name="algorithm">The <see cref="HashAlgorithmName"/> to use for hashing (e.g., SHA256, MD5).</param>
    /// <returns>A hexadecimal string representation of the computed hash.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the specified hash algorithm cannot be created.</exception>
    public static string Generate(string content, HashAlgorithmName algorithm)
    {
        using (var hashAlgorithm = HashAlgorithm.Create(algorithm.Name))
        {
            if (hashAlgorithm == null) throw new InvalidOperationException("Unable to create hash algorithm.");

            var hash = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(content));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}