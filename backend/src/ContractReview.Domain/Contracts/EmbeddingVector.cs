using System.Buffers.Binary;

namespace ContractReview.Domain.Contracts;

/// <summary>Converts embedding vectors to and from their stored form: float32 values, little-endian.</summary>
public static class EmbeddingVector
{
    public const int BytesPerValue = sizeof(float);

    public static byte[] ToBytes(ReadOnlySpan<float> vector)
    {
        var bytes = new byte[vector.Length * BytesPerValue];
        for (var i = 0; i < vector.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * BytesPerValue), vector[i]);
        }

        return bytes;
    }

    public static float[] FromBytes(ReadOnlySpan<byte> bytes)
    {
        var vector = new float[bytes.Length / BytesPerValue];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes[(i * BytesPerValue)..]);
        }

        return vector;
    }
}
