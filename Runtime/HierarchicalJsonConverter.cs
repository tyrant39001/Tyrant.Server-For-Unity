#if !EXCLUDEJSONEXTENTION

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Tyrant.GameCore;

namespace Tyrant.Server.JsonExtention
{
    public class NewtonJsonSerializer : IJsonSerializer
    {
        object IJsonSerializer.DeserializeObject(string value) => JsonConvert.DeserializeObject(value);
        string IJsonSerializer.HierarchicalSerializeObject(object obj) => JsonConvert.SerializeObject(obj, new HierarchicalJsonConverter());
        string IJsonSerializer.SerializeObject(object obj) => JsonConvert.SerializeObject(obj);
        void IJsonSerializer.SerializeToTextWriter(TextWriter textWriter, object obj) => new JsonSerializer { Formatting = Formatting.Indented }.Serialize(textWriter, obj);
        object IJsonSerializer.DeserializeFromTextReader(TextReader textWriter, Type objType) => new JsonSerializer { Formatting = Formatting.Indented }.Deserialize(textWriter, objType);
    }

    /// <summary>
    /// 层次化的Json转换器，不展开集合和复杂类型属性的值
    /// </summary>
    public class HierarchicalJsonConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var resolve = serializer.ContractResolver;
            var contract = resolve.ResolveContract(value.GetType());
            if (contract is JsonObjectContract objectContract)
            {
                writer.WriteStartObject();
                foreach (var property in objectContract.Properties)
                {
                    if (property.Ignored)
                        continue;

                    var childValue = property.ValueProvider.GetValue(value);
                    var propertyContract = resolve.ResolveContract(childValue?.GetType() ?? property.PropertyType);
                    if (propertyContract is JsonObjectContract)
                    {
                        serializer.Serialize(writer, childValue);
                    }
                    else if (propertyContract is JsonArrayContract)
                    {
                        var childCollection = (ICollection)childValue;
                        // 特殊处理
                        var name = $"{property.PropertyName},{childCollection.Count}";
                        writer.WritePropertyName(name);
                        writer.WriteStartArray();
                        writer.WriteEndArray();
                    }
                    else
                    {
                        writer.WritePropertyName(property.PropertyName);
                        writer.WriteValue(childValue);
                    }
                }
                writer.WriteEndObject();
            }
            else if (contract is JsonArrayContract arrayContract)
            {
                writer.WriteStartArray();
                var enumerator = ((IEnumerable)value).GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current == null)
                    {
                        writer.WriteNull();
                        continue;
                    }
                    var type = enumerator.Current.GetType();
                    if (type.IsValueType || type == typeof(string))
                    {
                        writer.WriteValue(enumerator.Current);
                    }
                    else
                    {
                        serializer.Serialize(writer, enumerator.Current);
                    }
                }
                writer.WriteEndArray();
            }
            else
            {
                writer.WriteValue(value);
            }

            //var jToken = JToken.FromObject(value);
            //switch (jToken)
            //{
            //    case JValue jValue:
            //        writer.WriteValue(jValue);
            //        break;
            //    case JObject jObject:
            //        writer.WriteStartObject();
            //        foreach (var jTokenChild in jObject)
            //        {
            //            if (!CanConvert(jTokenChild.Value.GetType()))
            //                continue;
            //            switch (jTokenChild.Value)
            //            {
            //                case JValue jValue:
            //                    writer.WritePropertyName(jTokenChild.Key);
            //                    writer.WriteValue(jValue);
            //                    break;
            //                case JObject jObj:
            //                    serializer.Serialize(writer, jObj);
            //                    break;
            //                case JArray jArrayChild:
            //                    //特殊处理
            //                    var name = $"{jTokenChild.Key},{jArrayChild.Count}";
            //                    writer.WritePropertyName(name);
            //                    writer.WriteStartArray();
            //                    writer.WriteEndArray();
            //                    break;
            //            }
            //        }
            //        writer.WriteEndObject();
            //        break;
            //    case JArray jArray:
            //        writer.WriteStartArray();
            //        foreach (var jItem in jArray)
            //        {
            //            switch (jItem)
            //            {
            //                case JValue jValue:
            //                    writer.WriteValue(jValue);
            //                    break;
            //                case JObject jObj:
            //                    serializer.Serialize(writer, jObj);
            //                    break;
            //                case JArray jArrayChild:
            //                    serializer.Serialize(writer, jArrayChild);
            //                    break;
            //            }
            //        }
            //        writer.WriteEndArray();
            //        break;
            //}
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);

            var target = Activator.CreateInstance(objectType);
            serializer.Populate(jObject.CreateReader(), target);
            return target;
        }

        public override bool CanConvert(Type objectType)
        {
            return true;
        }
    }
}

#endif