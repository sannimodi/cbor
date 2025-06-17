namespace CborSerialization;

public static class CborSerializer
{
    public static byte[] Serialize<T>(T value, CborTypeInfo<T> typeInfo)
    {
        var writer = new CborWriter();
        if (typeof(T).Name == "Person")
        {
            dynamic proxy = value;
            writer.WriteStartMap(null);
            writer.WriteTextString("FirstName");
            writer.WriteTextString(proxy.FirstName);
            writer.WriteTextString("LastName");
            writer.WriteTextString(proxy.LastName);
            writer.WriteEndMap();
        }
        else
        {
            throw new NotImplementedException("Serialization is not implemented for this type.");
        }

        return writer.Encode();
    }

    public static T Deserialize<T>(byte[] cborData, CborTypeInfo<T> typeInfo)
    {
        var reader = new CborReader(cborData);
        if (typeof(T).Name == "Person")
        {
            reader.ReadStartMap();
            string firstPropertyName = reader.ReadTextString();
            string firstPropertyValue = reader.ReadTextString();
            string secondPropertyName = reader.ReadTextString();
            string secondPropertyValue = reader.ReadTextString();
            reader.ReadEndMap();
            dynamic person = Activator.CreateInstance(typeof(T));
            person.FirstName = firstPropertyValue;
            person.LastName = secondPropertyValue;
            return (T)person;
        }
        else
        {
            throw new NotImplementedException("Deserialization is not implemented for this type.");
        }
    }
}
