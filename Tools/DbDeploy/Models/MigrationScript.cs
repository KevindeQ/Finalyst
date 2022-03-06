using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbDeploy.Models;

public enum MigrationOp
{
    Migrate,
    Undo
}

internal static class MigrationOpHelpers
{
    public static MigrationOp Parse(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException($"'{nameof(value)}' can't be null or empty.");
        }

        if (value.ToLower() == "m")
        {
            return MigrationOp.Migrate;
        }
        else if (value.ToLower() == "u")
        {
            return MigrationOp.Undo;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}

public class MigrationScript : IEquatable<MigrationScript>
{
    public int Id { get; set; }
    public MigrationOp Operation { get; set; }
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;

    // SHA256 checksum of file contents
    public byte[] FileChecksum { get; set; } = new byte[64];

    public bool Equals(MigrationScript? other)
    {
        if (Object.ReferenceEquals(other, null))
        {
            return false;
        }

        if (Object.ReferenceEquals(this, other))
        {
            return true;
        }

        return
            (Filename == other.Filename) &&
            FileChecksum.SequenceEqual(other.FileChecksum);
    }
}
