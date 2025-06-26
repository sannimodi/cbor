namespace NCborSample.Domain;

/// <summary>
/// Nested class for testing complex object serialization
/// </summary>
public class Address
{
    public string Street { get; set; } = string.Empty;
  
    public string City { get; set; } = string.Empty;
    
    public string? PostalCode { get; set; }
    
    public Dictionary<string, string> Coordinates { get; set; } = [];

    public static void Serialize(CborWriter writer, Address address)
    {
        writer.WriteStartMap(null);
        
        // Street
        writer.WriteTextString("Street");
        writer.WriteTextString(address.Street);
        
        // City
        writer.WriteTextString("City");
        writer.WriteTextString(address.City);
        
        // PostalCode (nullable)
        writer.WriteTextString("PostalCode");
        if (address.PostalCode != null)
        {
            writer.WriteTextString(address.PostalCode);
        }
        else
        {
            writer.WriteNull();
        }
        
        // Coordinates (Dictionary<string, string>)
        writer.WriteTextString("Coordinates");
        writer.WriteStartMap(address.Coordinates.Count);
        foreach (var kvp in address.Coordinates)
        {
            writer.WriteTextString(kvp.Key);
            writer.WriteTextString(kvp.Value);
        }
        writer.WriteEndMap();
        
        writer.WriteEndMap();
    }

    public static Address Deserialize(CborReader reader)
    {
        var address = new Address();
        
        reader.ReadStartMap();
        
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var propertyName = reader.ReadTextString();
            
            switch (propertyName)
            {
                case "Street":
                    address.Street = reader.ReadTextString();
                    break;
                case "City":
                    address.City = reader.ReadTextString();
                    break;
                case "PostalCode":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        address.PostalCode = null;
                    }
                    else
                    {
                        address.PostalCode = reader.ReadTextString();
                    }
                    break;
                case "Coordinates":
                    var coordinates = new Dictionary<string, string>();
                    int? mapSize = reader.ReadStartMap();
                    for (int i = 0; mapSize == null || i < mapSize; i++)
                    {
                        if (mapSize == null && reader.PeekState() == CborReaderState.EndMap) break;
                        var key = reader.ReadTextString();
                        var value = reader.ReadTextString();
                        coordinates.Add(key, value);
                    }
                    reader.ReadEndMap();
                    address.Coordinates = coordinates;
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }
        
        reader.ReadEndMap();
        return address;
    }
}
