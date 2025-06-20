using System.Formats.Cbor;

namespace CbOrSerialization.Tests;

public class SimpleDecimalTest
{
    [Fact]
    public void DirectDecimalSerialization_ShouldWork()
    {
        // Test direct CBOR decimal serialization without our library
        var testValue = 123.456m;
        
        // Serialize
        var writer = new CborWriter();
        writer.WriteDecimal(testValue);
        var serialized = writer.Encode();
        
        // Deserialize
        var reader = new CborReader(serialized);
        var deserialized = reader.ReadDecimal();
        
        // Assert
        deserialized.Should().Be(testValue);
    }
    
    [Fact]
    public void DirectNullableDecimalSerialization_ShouldWork()
    {
        // Test nullable decimal
        decimal? testValue = 456.789m;
        
        // Serialize
        var writer = new CborWriter();
        if (testValue.HasValue)
        {
            writer.WriteDecimal(testValue.Value);
        }
        else
        {
            writer.WriteNull();
        }
        var serialized = writer.Encode();
        
        // Deserialize
        var reader = new CborReader(serialized);
        decimal? deserialized;
        if (reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            deserialized = null;
        }
        else
        {
            deserialized = reader.ReadDecimal();
        }
        
        // Assert
        deserialized.Should().Be(testValue);
    }
}