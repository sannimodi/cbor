using System;
using System.Formats.Cbor;
using CborSerialization;

namespace CborSerialization.Demo;

class Program
{
    static void Main(string[] args)
    {
        var person = new Person
        {
            FirstName = "John",
            LastName = "Doe",
            Age = 30
        };


    }
}