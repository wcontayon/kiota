using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using Kiota.Abstractions.Serialization;

namespace KiotaCore.Serialization {
    public class JsonParseNode : IParseNode {
        private readonly JsonElement _jsonNode;
        public JsonParseNode(JsonElement node)
        {
            _jsonNode = node;
        }
        public string GetStringValue() => _jsonNode.GetString();
        public bool GetBoolValue() => _jsonNode.GetBoolean();
        public int GetIntValue() => _jsonNode.GetInt32();
        public decimal GetFloatValue() => _jsonNode.GetDecimal();
        public double GetDoubleValue() => _jsonNode.GetDouble();
        public Guid GetGuidValue() => _jsonNode.GetGuid();
        public DateTimeOffset GetDateTimeOffsetValue() => _jsonNode.GetDateTimeOffset();
        public IEnumerable<T> GetCollectionOfObjectValues<T>() where T: class, IParsable<T>, new() {
            var enumerator = _jsonNode.EnumerateArray();
            while(enumerator.MoveNext()) {
                var currentParseNode = new JsonParseNode(enumerator.Current);
                yield return currentParseNode.GetObjectValue<T>();
            }
        }
        public IEnumerable<T> GetCollectionOfPrimitiveValues<T>() {
            var enumerator = _jsonNode.EnumerateArray();
            while(enumerator.MoveNext()) {
                var currentParseNode = new JsonParseNode(enumerator.Current);
                var genericType = typeof(T);
                if(genericType == typeof(bool))
                    yield return (T)(object)currentParseNode.GetBoolValue();
                else if(genericType == typeof(string))
                    yield return (T)(object)currentParseNode.GetStringValue();
                else if(genericType == typeof(int))
                    yield return (T)(object)currentParseNode.GetIntValue();
                else if(genericType == typeof(float))
                    yield return (T)(object)currentParseNode.GetFloatValue();
                else if(genericType == typeof(double))
                    yield return (T)(object)currentParseNode.GetDoubleValue();
                else if(genericType == typeof(Guid))
                    yield return (T)(object)currentParseNode.GetGuidValue();
                else if(genericType == typeof(DateTimeOffset))
                    yield return (T)(object)currentParseNode.GetGuidValue();
                else
                    throw new InvalidOperationException($"unknown type for deserialization {genericType.FullName}");
            }
        }
        public T GetObjectValue<T>() where T: class, IParsable<T>, new() {
            var item = new T();
            foreach(var field in item.DeserializeFields) { //TODO walk parent types
                Debug.WriteLine($"getting property {field.Key}");
                try {
                    var fieldValue = _jsonNode.GetProperty(field.Key);
                    if(fieldValue.ValueKind != JsonValueKind.Null) {
                        field.Value.Invoke(item, new JsonParseNode(fieldValue));
                    }
                } catch(KeyNotFoundException) {
                    Debug.WriteLine($"couldn't find property {field.Key}");
                }
            }
            return item;
        }
        public IParseNode GetChildNode(string identifier) => new JsonParseNode(_jsonNode.GetProperty(identifier ?? throw new ArgumentNullException(nameof(identifier))));
    }
}
