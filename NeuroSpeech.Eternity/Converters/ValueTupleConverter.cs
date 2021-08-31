using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeuroSpeech.Eternity.Converters
{
    public class ValueTupleConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            if (!typeToConvert.IsConstructedGenericType)
                return false;
            var td = typeToConvert.GetGenericTypeDefinition();
            switch (td.GetGenericArguments().Length)
            {
                case 1:
                    return td == typeof(ValueTuple<>);
                case 2:
                    return td == typeof(ValueTuple<,>);
                case 3:
                    return td == typeof(ValueTuple<,,>);
                case 4:
                    return td == typeof(ValueTuple<,,,>);
                case 5:
                    return td == typeof(ValueTuple<,,,,>);
                case 6:
                    return td == typeof(ValueTuple<,,,,,>);
                case 7:
                    return td == typeof(ValueTuple<,,,,,,>);
                case 8:
                    return td == typeof(ValueTuple<,,,,,,,>);
            }

            return false;
        }

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            if (!CanConvert(typeToConvert))
                return null;
            var t = typeof(FieldConverter<>).MakeGenericType(typeToConvert);
            return Activator.CreateInstance(t) as JsonConverter;
        }
    }

    public class FieldConverter<T> : JsonConverter<T>
        where T: new()
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException($"Expecting an object but found {reader.TokenType}");
            var value = (object)new T();
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var name = reader.GetString();
                        if (!reader.Read())
                        {
                            throw new JsonException();
                        }
                        var fieldType = typeToConvert.GetField(name);
                        if(fieldType == null)
                        {
                            reader.Read();
                            continue;
                        }
                        var fv = JsonSerializer.Deserialize(ref reader, fieldType.FieldType, options);
                        fieldType.SetValue(value, fv);
                        continue;
                    case JsonTokenType.EndObject:
                        return (T)value;
                }
                throw new JsonException();
            }
            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach(var f in typeof(T).GetFields())
            {
                var v = f.GetValue(value);
                if (v == null)
                {
                    continue;
                }
                writer.WritePropertyName(f.Name);
                JsonSerializer.Serialize(writer, v, f.FieldType, options);
            }
            writer.WriteEndObject();
        }
    }

    
}
