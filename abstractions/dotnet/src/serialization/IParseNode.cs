using System;
using System.Collections.Generic;

namespace Microsoft.Kiota.Abstractions.Serialization {
    public interface IParseNode {
        string GetStringValue();
        IParseNode GetChildNode(string identifier);
        bool? GetBoolValue();
        int? GetIntValue();
        decimal? GetFloatValue();
        double? GetDoubleValue();
        Guid? GetGuidValue();
        DateTimeOffset? GetDateTimeOffsetValue();
        IEnumerable<T> GetCollectionOfPrimitiveValues<T>();
        IEnumerable<T> GetCollectionOfObjectValues<T>() where T: class, IParsable<T>, new();
        T? GetEnumValue<T>() where T: struct, Enum;
        T GetObjectValue<T>() where T: class, IParsable<T>, new();
    }
}
