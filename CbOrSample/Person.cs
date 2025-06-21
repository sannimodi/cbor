using System.Formats.Cbor;

/// <summary>
/// Comprehensive Person class demonstrating all supported data types
/// </summary>
public partial class Person
{
    // Primitive types
    public required string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public double Height { get; set; }
    public float Weight { get; set; }
    public long Id { get; set; }
    public byte Level { get; set; }
    public sbyte Score { get; set; }
    public short RankShort { get; set; }
    public ushort RankUShort { get; set; }
    public uint Points { get; set; }
    public ulong BigNumber { get; set; }

    // Decimal type (newly implemented)
    public decimal Salary { get; set; }
    public decimal? Bonus { get; set; }

    // DateTime types
    public DateTime CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTimeOffset? LastModified { get; set; }

    // Guid type
    public Guid PersonId { get; set; }
    public Guid? SessionId { get; set; }

    // Collections (Lists)
    public List<string> Tags { get; set; } = new();
    public List<int> Scores { get; set; } = new();
    public List<string>? OptionalNotes { get; set; }
    
    // Arrays
    public string[] Skills { get; set; } = Array.Empty<string>();
    public int[] TestScores { get; set; } = Array.Empty<int>();
    public double[] Measurements { get; set; } = Array.Empty<double>();
    public string[]? OptionalTags { get; set; }
    public Address[]? PreviousAddresses { get; set; }

    // Dictionaries
    public Dictionary<string, string> Metadata { get; set; } = new();
    public Dictionary<string, int> CategoryScores { get; set; } = new();
    public Dictionary<string, string>? OptionalData { get; set; }

    // Nullable primitives
    public int? OptionalAge { get; set; }
    public bool? IsVerified { get; set; }
    public double? OptionalHeight { get; set; }
    public float? OptionalWeight { get; set; }

    // Nested object
    public Address? HomeAddress { get; set; }

    // Manual serialization methods (to be implemented)
    public static void Serialize(CborWriter writer, Person person)
    {
        writer.WriteStartMap(null);
        
        // Primitive types
        writer.WriteTextString("Name");
        writer.WriteTextString(person.Name);
        
        writer.WriteTextString("Age");
        writer.WriteInt32(person.Age);
        
        writer.WriteTextString("IsActive");
        writer.WriteBoolean(person.IsActive);
        
        writer.WriteTextString("Height");
        writer.WriteDouble(person.Height);
        
        writer.WriteTextString("Weight");
        writer.WriteSingle(person.Weight);
        
        writer.WriteTextString("Id");
        writer.WriteInt64(person.Id);
        
        writer.WriteTextString("Level");
        writer.WriteUInt32((uint)person.Level);
        
        writer.WriteTextString("Score");
        writer.WriteInt32(person.Score);
        
        writer.WriteTextString("RankShort");
        writer.WriteInt32(person.RankShort);
        
        writer.WriteTextString("RankUShort");
        writer.WriteUInt32(person.RankUShort);
        
        writer.WriteTextString("Points");
        writer.WriteUInt32(person.Points);
        
        writer.WriteTextString("BigNumber");
        writer.WriteUInt64(person.BigNumber);
        
        // Decimal types
        writer.WriteTextString("Salary");
        writer.WriteDecimal(person.Salary);
        
        writer.WriteTextString("Bonus");
        if (person.Bonus.HasValue)
        {
            writer.WriteDecimal(person.Bonus.Value);
        }
        else
        {
            writer.WriteNull();
        }
        
        // DateTime types
        writer.WriteTextString("CreatedAt");
        writer.WriteTag(CborTag.DateTimeString);
        writer.WriteTextString(person.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFK", System.Globalization.CultureInfo.InvariantCulture));
        
        writer.WriteTextString("UpdatedAt");
        writer.WriteTag(CborTag.DateTimeString);
        writer.WriteTextString(person.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFK", System.Globalization.CultureInfo.InvariantCulture));
        
        writer.WriteTextString("LastLogin");
        if (person.LastLogin.HasValue)
        {
            writer.WriteTag(CborTag.DateTimeString);
            writer.WriteTextString(person.LastLogin.Value.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFK", System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteNull();
        }
        
        writer.WriteTextString("LastModified");
        if (person.LastModified.HasValue)
        {
            writer.WriteTag(CborTag.DateTimeString);
            writer.WriteTextString(person.LastModified.Value.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFK", System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteNull();
        }
        
        // Guid types
        writer.WriteTextString("PersonId");
        writer.WriteByteString(person.PersonId.ToByteArray());
        
        writer.WriteTextString("SessionId");
        if (person.SessionId.HasValue)
        {
            writer.WriteByteString(person.SessionId.Value.ToByteArray());
        }
        else
        {
            writer.WriteNull();
        }
        
        // Collections
        writer.WriteTextString("Tags");
        writer.WriteStartArray(person.Tags.Count);
        foreach (var tag in person.Tags)
        {
            writer.WriteTextString(tag);
        }
        writer.WriteEndArray();
        
        writer.WriteTextString("Scores");
        writer.WriteStartArray(person.Scores.Count);
        foreach (var score in person.Scores)
        {
            writer.WriteInt32(score);
        }
        writer.WriteEndArray();
        
        writer.WriteTextString("OptionalNotes");
        if (person.OptionalNotes != null)
        {
            writer.WriteStartArray(person.OptionalNotes.Count);
            foreach (var note in person.OptionalNotes)
            {
                writer.WriteTextString(note);
            }
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteNull();
        }
        
        // Arrays
        writer.WriteTextString("Skills");
        writer.WriteStartArray(person.Skills.Length);
        foreach (var skill in person.Skills)
        {
            writer.WriteTextString(skill);
        }
        writer.WriteEndArray();
        
        writer.WriteTextString("TestScores");
        writer.WriteStartArray(person.TestScores.Length);
        foreach (var score in person.TestScores)
        {
            writer.WriteInt32(score);
        }
        writer.WriteEndArray();
        
        writer.WriteTextString("Measurements");
        writer.WriteStartArray(person.Measurements.Length);
        foreach (var measurement in person.Measurements)
        {
            writer.WriteDouble(measurement);
        }
        writer.WriteEndArray();
        
        writer.WriteTextString("OptionalTags");
        if (person.OptionalTags != null)
        {
            writer.WriteStartArray(person.OptionalTags.Length);
            foreach (var tag in person.OptionalTags)
            {
                writer.WriteTextString(tag);
            }
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteNull();
        }
        
        writer.WriteTextString("PreviousAddresses");
        if (person.PreviousAddresses != null)
        {
            writer.WriteStartArray(person.PreviousAddresses.Length);
            foreach (var address in person.PreviousAddresses)
            {
                Address.Serialize(writer, address);
            }
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteNull();
        }
        
        // Dictionaries
        writer.WriteTextString("Metadata");
        writer.WriteStartMap(person.Metadata.Count);
        foreach (var kvp in person.Metadata)
        {
            writer.WriteTextString(kvp.Key);
            writer.WriteTextString(kvp.Value);
        }
        writer.WriteEndMap();
        
        writer.WriteTextString("CategoryScores");
        writer.WriteStartMap(person.CategoryScores.Count);
        foreach (var kvp in person.CategoryScores)
        {
            writer.WriteTextString(kvp.Key);
            writer.WriteInt32(kvp.Value);
        }
        writer.WriteEndMap();
        
        writer.WriteTextString("OptionalData");
        if (person.OptionalData != null)
        {
            writer.WriteStartMap(person.OptionalData.Count);
            foreach (var kvp in person.OptionalData)
            {
                writer.WriteTextString(kvp.Key);
                writer.WriteTextString(kvp.Value);
            }
            writer.WriteEndMap();
        }
        else
        {
            writer.WriteNull();
        }
        
        // Nullable primitives
        writer.WriteTextString("OptionalAge");
        if (person.OptionalAge.HasValue)
        {
            writer.WriteInt32(person.OptionalAge.Value);
        }
        else
        {
            writer.WriteNull();
        }
        
        writer.WriteTextString("IsVerified");
        if (person.IsVerified.HasValue)
        {
            writer.WriteBoolean(person.IsVerified.Value);
        }
        else
        {
            writer.WriteNull();
        }
        
        writer.WriteTextString("OptionalHeight");
        if (person.OptionalHeight.HasValue)
        {
            writer.WriteDouble(person.OptionalHeight.Value);
        }
        else
        {
            writer.WriteNull();
        }
        
        writer.WriteTextString("OptionalWeight");
        if (person.OptionalWeight.HasValue)
        {
            writer.WriteSingle(person.OptionalWeight.Value);
        }
        else
        {
            writer.WriteNull();
        }
        
        // Nested object
        writer.WriteTextString("HomeAddress");
        if (person.HomeAddress != null)
        {
            Address.Serialize(writer, person.HomeAddress);
        }
        else
        {
            writer.WriteNull();
        }
        
        writer.WriteEndMap();
    }

    public static Person Deserialize(CborReader reader)
    {
        var person = new Person { Name = "" }; // Required property
        
        reader.ReadStartMap();
        
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var propertyName = reader.ReadTextString();
            
            switch (propertyName)
            {
                // Primitive types
                case "Name":
                    person.Name = reader.ReadTextString();
                    break;
                case "Age":
                    person.Age = reader.ReadInt32();
                    break;
                case "IsActive":
                    person.IsActive = reader.ReadBoolean();
                    break;
                case "Height":
                    person.Height = reader.ReadDouble();
                    break;
                case "Weight":
                    person.Weight = reader.ReadSingle();
                    break;
                case "Id":
                    person.Id = reader.ReadInt64();
                    break;
                case "Level":
                    person.Level = (byte)reader.ReadUInt32();
                    break;
                case "Score":
                    person.Score = (sbyte)reader.ReadInt32();
                    break;
                case "RankShort":
                    person.RankShort = (short)reader.ReadInt32();
                    break;
                case "RankUShort":
                    person.RankUShort = (ushort)reader.ReadUInt32();
                    break;
                case "Points":
                    person.Points = reader.ReadUInt32();
                    break;
                case "BigNumber":
                    person.BigNumber = reader.ReadUInt64();
                    break;
                
                // Decimal types
                case "Salary":
                    person.Salary = reader.ReadDecimal();
                    break;
                case "Bonus":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        person.Bonus = null;
                    }
                    else
                    {
                        person.Bonus = reader.ReadDecimal();
                    }
                    break;
                
                // DateTime types
                case "CreatedAt":
                    var createdAtTag = reader.ReadTag();
                    if (createdAtTag == CborTag.DateTimeString)
                    {
                        person.CreatedAt = DateTime.ParseExact(reader.ReadTextString(), "yyyy-MM-ddTHH:mm:ss.FFFFFFFK", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
                    }
                    else
                    {
                        throw new InvalidOperationException("Expected DateTimeString tag");
                    }
                    break;
                case "UpdatedAt":
                    var updatedAtTag = reader.ReadTag();
                    if (updatedAtTag == CborTag.DateTimeString)
                    {
                        person.UpdatedAt = DateTimeOffset.ParseExact(reader.ReadTextString(), "yyyy-MM-ddTHH:mm:ss.FFFFFFFK", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
                    }
                    else
                    {
                        throw new InvalidOperationException("Expected DateTimeString tag");
                    }
                    break;
                case "LastLogin":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        person.LastLogin = null;
                    }
                    else
                    {
                        var lastLoginTag = reader.ReadTag();
                        if (lastLoginTag == CborTag.DateTimeString)
                        {
                            person.LastLogin = DateTime.ParseExact(reader.ReadTextString(), "yyyy-MM-ddTHH:mm:ss.FFFFFFFK", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
                        }
                        else
                        {
                            throw new InvalidOperationException("Expected DateTimeString tag");
                        }
                    }
                    break;
                case "LastModified":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        person.LastModified = null;
                    }
                    else
                    {
                        var lastModifiedTag = reader.ReadTag();
                        if (lastModifiedTag == CborTag.DateTimeString)
                        {
                            person.LastModified = DateTimeOffset.ParseExact(reader.ReadTextString(), "yyyy-MM-ddTHH:mm:ss.FFFFFFFK", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
                        }
                        else
                        {
                            throw new InvalidOperationException("Expected DateTimeString tag");
                        }
                    }
                    break;
                
                // Guid types
                case "PersonId":
                    person.PersonId = new Guid(reader.ReadByteString());
                    break;
                case "SessionId":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        person.SessionId = null;
                    }
                    else
                    {
                        person.SessionId = new Guid(reader.ReadByteString());
                    }
                    break;
                
                // Collections
                case "Tags":
                    var tags = new List<string>();
                    int? tagsArraySize = reader.ReadStartArray();
                    for (int i = 0; tagsArraySize == null || i < tagsArraySize; i++)
                    {
                        if (tagsArraySize == null && reader.PeekState() == CborReaderState.EndArray) break;
                        tags.Add(reader.ReadTextString());
                    }
                    reader.ReadEndArray();
                    person.Tags = tags;
                    break;
                case "Scores":
                    var scores = new List<int>();
                    int? scoresArraySize = reader.ReadStartArray();
                    for (int i = 0; scoresArraySize == null || i < scoresArraySize; i++)
                    {
                        if (scoresArraySize == null && reader.PeekState() == CborReaderState.EndArray) break;
                        scores.Add(reader.ReadInt32());
                    }
                    reader.ReadEndArray();
                    person.Scores = scores;
                    break;
                case "OptionalNotes":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        person.OptionalNotes = null;
                    }
                    else
                    {
                        var notes = new List<string>();
                        int? notesArraySize = reader.ReadStartArray();
                        for (int i = 0; notesArraySize == null || i < notesArraySize; i++)
                        {
                            if (notesArraySize == null && reader.PeekState() == CborReaderState.EndArray) break;
                            notes.Add(reader.ReadTextString());
                        }
                        reader.ReadEndArray();
                        person.OptionalNotes = notes;
                    }
                    break;
                
                // Arrays
                case "Skills":
                    var skillsList = new List<string>();
                    int? skillsArraySize = reader.ReadStartArray();
                    for (int i = 0; skillsArraySize == null || i < skillsArraySize; i++)
                    {
                        if (skillsArraySize == null && reader.PeekState() == CborReaderState.EndArray) break;
                        skillsList.Add(reader.ReadTextString());
                    }
                    reader.ReadEndArray();
                    person.Skills = skillsList.ToArray();
                    break;
                case "TestScores":
                    var testScoresList = new List<int>();
                    int? testScoresArraySize = reader.ReadStartArray();
                    for (int i = 0; testScoresArraySize == null || i < testScoresArraySize; i++)
                    {
                        if (testScoresArraySize == null && reader.PeekState() == CborReaderState.EndArray) break;
                        testScoresList.Add(reader.ReadInt32());
                    }
                    reader.ReadEndArray();
                    person.TestScores = testScoresList.ToArray();
                    break;
                case "Measurements":
                    var measurementsList = new List<double>();
                    int? measurementsArraySize = reader.ReadStartArray();
                    for (int i = 0; measurementsArraySize == null || i < measurementsArraySize; i++)
                    {
                        if (measurementsArraySize == null && reader.PeekState() == CborReaderState.EndArray) break;
                        measurementsList.Add(reader.ReadDouble());
                    }
                    reader.ReadEndArray();
                    person.Measurements = measurementsList.ToArray();
                    break;
                case "OptionalTags":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        person.OptionalTags = null;
                    }
                    else
                    {
                        var optionalTagsList = new List<string>();
                        int? optionalTagsArraySize = reader.ReadStartArray();
                        for (int i = 0; optionalTagsArraySize == null || i < optionalTagsArraySize; i++)
                        {
                            if (optionalTagsArraySize == null && reader.PeekState() == CborReaderState.EndArray) break;
                            optionalTagsList.Add(reader.ReadTextString());
                        }
                        reader.ReadEndArray();
                        person.OptionalTags = optionalTagsList.ToArray();
                    }
                    break;
                case "PreviousAddresses":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        person.PreviousAddresses = null;
                    }
                    else
                    {
                        var previousAddressesList = new List<Address>();
                        int? previousAddressesArraySize = reader.ReadStartArray();
                        for (int i = 0; previousAddressesArraySize == null || i < previousAddressesArraySize; i++)
                        {
                            if (previousAddressesArraySize == null && reader.PeekState() == CborReaderState.EndArray) break;
                            previousAddressesList.Add(Address.Deserialize(reader));
                        }
                        reader.ReadEndArray();
                        person.PreviousAddresses = previousAddressesList.ToArray();
                    }
                    break;
                
                // Dictionaries
                case "Metadata":
                    var metadata = new Dictionary<string, string>();
                    int? metadataMapSize = reader.ReadStartMap();
                    for (int i = 0; metadataMapSize == null || i < metadataMapSize; i++)
                    {
                        if (metadataMapSize == null && reader.PeekState() == CborReaderState.EndMap) break;
                        var key = reader.ReadTextString();
                        var value = reader.ReadTextString();
                        metadata.Add(key, value);
                    }
                    reader.ReadEndMap();
                    person.Metadata = metadata;
                    break;
                case "CategoryScores":
                    var categoryScores = new Dictionary<string, int>();
                    int? categoryScoresMapSize = reader.ReadStartMap();
                    for (int i = 0; categoryScoresMapSize == null || i < categoryScoresMapSize; i++)
                    {
                        if (categoryScoresMapSize == null && reader.PeekState() == CborReaderState.EndMap) break;
                        var key = reader.ReadTextString();
                        var value = reader.ReadInt32();
                        categoryScores.Add(key, value);
                    }
                    reader.ReadEndMap();
                    person.CategoryScores = categoryScores;
                    break;
                case "OptionalData":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        person.OptionalData = null;
                    }
                    else
                    {
                        var optionalData = new Dictionary<string, string>();
                        int? optionalDataMapSize = reader.ReadStartMap();
                        for (int i = 0; optionalDataMapSize == null || i < optionalDataMapSize; i++)
                        {
                            if (optionalDataMapSize == null && reader.PeekState() == CborReaderState.EndMap) break;
                            var key = reader.ReadTextString();
                            var value = reader.ReadTextString();
                            optionalData.Add(key, value);
                        }
                        reader.ReadEndMap();
                        person.OptionalData = optionalData;
                    }
                    break;
                
                // Nullable primitives
                case "OptionalAge":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        person.OptionalAge = null;
                    }
                    else
                    {
                        person.OptionalAge = reader.ReadInt32();
                    }
                    break;
                case "IsVerified":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        person.IsVerified = null;
                    }
                    else
                    {
                        person.IsVerified = reader.ReadBoolean();
                    }
                    break;
                case "OptionalHeight":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        person.OptionalHeight = null;
                    }
                    else
                    {
                        person.OptionalHeight = reader.ReadDouble();
                    }
                    break;
                case "OptionalWeight":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        person.OptionalWeight = null;
                    }
                    else
                    {
                        person.OptionalWeight = reader.ReadSingle();
                    }
                    break;
                
                // Nested object
                case "HomeAddress":
                    if (reader.PeekState() == CborReaderState.Null)
                    {
                        reader.ReadNull();
                        person.HomeAddress = null;
                    }
                    else
                    {
                        person.HomeAddress = Address.Deserialize(reader);
                    }
                    break;
                
                default:
                    reader.SkipValue();
                    break;
            }
        }
        
        reader.ReadEndMap();
        return person;
    }
}

/// <summary>
/// Nested class for testing complex object serialization
/// </summary>
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public Dictionary<string, string> Coordinates { get; set; } = new();

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