using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Kiota.Abstractions.Serialization;

namespace Microsoft.Kiota.Serialization.Json {
    public class JsonParseNode : IParseNode {
        private readonly JsonElement _jsonNode;
        public JsonParseNode(JsonElement node)
        {
            _jsonNode = node;
        }
        public string GetStringValue() => _jsonNode.GetString();
        public bool? GetBoolValue() => _jsonNode.GetBoolean();
        public int? GetIntValue() => _jsonNode.GetInt32();
        public decimal? GetFloatValue() => _jsonNode.GetDecimal();
        public double? GetDoubleValue() => _jsonNode.GetDouble();
        public Guid? GetGuidValue() => _jsonNode.GetGuid();
        public DateTimeOffset? GetDateTimeOffsetValue() => _jsonNode.GetDateTimeOffset();
        public T? GetEnumValue<T>() where T: struct, Enum {
            var rawValue = _jsonNode.GetString();
            if(string.IsNullOrEmpty(rawValue)) return default;
            if(typeof(T).GetCustomAttributes<FlagsAttribute>().Any()) {
                return (T)(object)rawValue
                    .Split(',')
                    .Select(x => Enum.Parse<T>(x, true))
                    .Select(x => (int)(object)x)
                    .Sum();
            } else
                return Enum.Parse<T>(rawValue, true);
        }
        public IEnumerable<T> GetCollectionOfObjectValues<T>() where T: class, IParsable<T>, new() {
            var enumerator = _jsonNode.EnumerateArray();
            while(enumerator.MoveNext()) {
                var currentParseNode = new JsonParseNode(enumerator.Current);
                yield return currentParseNode.GetObjectValue<T>();
            }
        }
        private static Type booleanType = typeof(bool?);
        private static Type stringType = typeof(string);
        private static Type intType = typeof(int?);
        private static Type floatType = typeof(float?);
        private static Type doubleType = typeof(double?);
        private static Type guidType = typeof(Guid?);
        private static Type dateTimeOffsetType = typeof(DateTimeOffset?);
        public IEnumerable<T> GetCollectionOfPrimitiveValues<T>() {
            var genericType = typeof(T);
            foreach(var collectionValue in _jsonNode.EnumerateArray()) {
                var currentParseNode = new JsonParseNode(collectionValue);
                if(genericType == booleanType)
                    yield return (T)(object)currentParseNode.GetBoolValue();
                else if(genericType == stringType)
                    yield return (T)(object)currentParseNode.GetStringValue();
                else if(genericType == intType)
                    yield return (T)(object)currentParseNode.GetIntValue();
                else if(genericType == floatType)
                    yield return (T)(object)currentParseNode.GetFloatValue();
                else if(genericType == doubleType)
                    yield return (T)(object)currentParseNode.GetDoubleValue();
                else if(genericType == guidType)
                    yield return (T)(object)currentParseNode.GetGuidValue();
                else if(genericType == dateTimeOffsetType)
                    yield return (T)(object)currentParseNode.GetDateTimeOffsetValue();
                else
                    throw new InvalidOperationException($"unknown type for deserialization {genericType.FullName}");
            }
        }
        private static Type objectType = typeof(object);
        public T GetObjectValue<T>() where T: class, IParsable<T>, new() {
            var item = new T();
            var fieldDeserializers = GetFieldDeserializers(item);
            AssignFieldValues(item, fieldDeserializers);
            return item;
        }
        private Dictionary<string, Action<T, IParseNode>> GetFieldDeserializers<T>(T item) where T: class, IParsable<T>, new() {
            //note: we might be able to save a lot of cycles by simply "caching" these dictionaries with their types in a static property
            var baseType = typeof(T).BaseType;
            var fieldDeserializers = new Dictionary<string, Action<T, IParseNode>>(item.DeserializeFields);
            while(baseType != null && baseType != objectType) {
                Debug.WriteLine($"setting property values for parent type {baseType.Name}");
                var baseTypeFieldsProperty = baseType.GetProperty(nameof(item.DeserializeFields), BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance);
                if(baseTypeFieldsProperty == null)
                    baseType = null;
                else {
                    var baseTypeFieldDeserializers = baseTypeFieldsProperty.GetValue(item) as IEnumerable<object>;
                    // cannot be cast to IDictionary<string, Action<T, IParseNode>> as action generic types are contra variant
                    // cheap lazy loading to avoid running reflection on every object of the collection when we know they are the same type
                    if(baseTypeFieldDeserializers?.Any() ?? false) {
                        Type baseFieldDeserializerType  = baseTypeFieldDeserializers.First().GetType();
                        PropertyInfo keyProperty = baseFieldDeserializerType.GetProperty("Key");
                        PropertyInfo valuePropery = baseFieldDeserializerType.GetProperty("Value");
                        foreach(var baseTypeFieldDeserializer in baseTypeFieldDeserializers) {
                            var key = keyProperty.GetValue(baseTypeFieldDeserializer) as string;
                            var action = valuePropery.GetValue(baseTypeFieldDeserializer) as Action<T, IParseNode>;
                            fieldDeserializers.Add(key, action);
                        }
                    }
                    baseType = baseType.BaseType;
                }
            }
            return fieldDeserializers;
        }
        private void AssignFieldValues<T>(T item, Dictionary<string, Action<T, IParseNode>> fieldDeserializers) where T: class, IParsable<T>, new() {
            if(_jsonNode.ValueKind != JsonValueKind.Object) return;

            foreach(var fieldValue in _jsonNode.EnumerateObject().Where(x => x.Value.ValueKind != JsonValueKind.Null)) {
                if(fieldDeserializers.ContainsKey(fieldValue.Name)) {
                    var fieldDeserializer = fieldDeserializers[fieldValue.Name];
                    Debug.WriteLine($"found property {fieldValue.Name} to deserialize");
                    fieldDeserializer.Invoke(item, new JsonParseNode(fieldValue.Value));
                } else {
                    Debug.WriteLine($"found additional property {fieldValue.Name} to deserialize");
                    item.AdditionalData.TryAdd(fieldValue.Name, TryGetAnything(fieldValue.Value));
                }
            }
        }
        private object TryGetAnything(JsonElement element) {
            switch(element.ValueKind) {
                case JsonValueKind.Number:
                    if(element.TryGetDecimal(out var dec)) return dec;
                    else if(element.TryGetDouble(out var db)) return db;
                    else if(element.TryGetInt16(out var s)) return s;
                    else if(element.TryGetInt32(out var i)) return i;
                    else if(element.TryGetInt64(out var l)) return l;
                    else if(element.TryGetSingle(out var f)) return f;
                    else if(element.TryGetUInt16(out var us)) return us;
                    else if(element.TryGetUInt32(out var ui)) return ui;
                    else if(element.TryGetUInt64(out var ul)) return ul;
                    else throw new InvalidOperationException("unexpected additional value type during number deserialization");
                case JsonValueKind.String:
                    if(element.TryGetDateTime(out var dt)) return dt;
                    else if(element.TryGetDateTimeOffset(out var dto)) return dto;
                    else if(element.TryGetGuid(out var g)) return g;
                    else return element.GetString();
                case JsonValueKind.Array:
                case JsonValueKind.Object:
                    return element;
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                default:
                    throw new InvalidOperationException($"unexpected additional value type during deserialization json kind : {element.ValueKind}");
            }
        }
        public IParseNode GetChildNode(string identifier) => new JsonParseNode(_jsonNode.GetProperty(identifier ?? throw new ArgumentNullException(nameof(identifier))));
    }
}
