using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using DotVVM.Framework.Utils;
using DotVVM.Framework.ViewModel.Serialization;
using FastExpressionCompiler;
// using Newtonsoft.Json.Linq;

namespace DotVVM.Framework.Diagnostics
{
    /// <summary> Computes the inclusive and exclusive size of each JSON property. </summary>
    public class JsonSizeAnalyzer
    {
        readonly IViewModelSerializationMapper viewModelMapper;

        public JsonSizeAnalyzer(IViewModelSerializationMapper viewModelMapper)
        {
            this.viewModelMapper = viewModelMapper;
        }
        /// <summary> Computes the inclusive and exclusive size of each JSON property. </summary>
        public JsonSizeProfile Analyze(JsonElement json)
        {
            throw new NotImplementedException(); // TODO
            // Dictionary<string, ClassSizeProfile> results = new();
            // // returns the length of the token. Recursively calls itself for arrays and objects.
            // AtomicSizeProfile analyzeToken(JsonElement token)
            // {
            //     switch (token.ValueKind)
            //     {
            //         case JsonValueKind.Object:
            //             return new (InclusiveSize: analyzeObject(token), ExclusiveSize: 2);
            //         case JsonValueKind.Array: {
            //             var r = new AtomicSizeProfile(0);
            //             foreach (var item in token.EnumerateArray())
            //             {
            //                 r += analyzeToken(item);
            //             }
            //             return r;
            //         }
            //         case JsonValueKind.String:
            //             return new ((((string?)token)?.Length ?? 4) + 2);
            //         case JsonValueKind.Integer:
            //             // This should be the same as token.ToString().Length, but I didn't want to allocate the string unnecesarily
            //             return new((int)Math.Log10(Math.Abs((long)token) + 1) + 1);
            //         case JsonValueKind.Number:
            //             return new(((double)token).ToString().Length);
            //         case JsonValueKind.True:
            //             return new(4);
            //         case JsonValueKind.False:
            //             return new(5);
            //         case JsonValueKind.Null:
            //             return new(4);
            //         default:
            //             Debug.Assert(false, $"Unexpected token type {token.ValueKind}");
            //             return new(token.ToString().Length);
            //     }
            // }
            // int analyzeObject(JsonElement j)
            // {
            //     var type = ((string?)j.GetPropertyOrNull("$type")?.GetString())?.Apply(viewModelMapper.GetMapByTypeId);

            //     var typeName = type?.Type.ToCode(stripNamespace: true) ?? "UnknownType";
            //     var props = new Dictionary<string, AtomicSizeProfile>();

            //     var totalSize = new AtomicSizeProfile(0);
            //     foreach (var prop in j.Properties())
            //     {
            //         var propSize = analyzeToken(prop.Value);
            //         props[prop.Name] = propSize;

            //         totalSize += propSize;
            //         totalSize += 4 + prop.Name.Length; // 2 for the quotes, 1 for :, 1 for ,
            //     }

            //     var classSize = new ClassSizeProfile(totalSize, props);
            //     if (results.TryGetValue(typeName, out var existing))
            //     {
            //         results[typeName] = existing + classSize;
            //     }
            //     else
            //     {
            //         results[typeName] = classSize;
            //     }
            //     return totalSize.InclusiveSize;
            // }

            // var totalSize = analyzeObject(json);
            // return new JsonSizeProfile(results, totalSize);
        }


        public record JsonSizeProfile(
            Dictionary<string, ClassSizeProfile> Classes,
            int TotalSize
        );
        public record ClassSizeProfile(
            AtomicSizeProfile Size,
            Dictionary<string, AtomicSizeProfile> Properties,
            int Count = 1
        ) {
            public static ClassSizeProfile operator +(ClassSizeProfile a, ClassSizeProfile b)
            {
                var props = new Dictionary<string, AtomicSizeProfile>(a.Properties);
                foreach (var prop in b.Properties)
                {
                    props[prop.Key] = props.GetValueOrDefault(prop.Key) + prop.Value;
                }
                return new(
                    a.Size + b.Size,
                    props,
                    a.Count + b.Count
                );
            }
        }
        public record struct AtomicSizeProfile(
            int InclusiveSize,
            int ExclusiveSize
        ) {
            public AtomicSizeProfile(int exclusiveSize): this(exclusiveSize, exclusiveSize) { }

            public static AtomicSizeProfile operator +(AtomicSizeProfile a, AtomicSizeProfile b) => new AtomicSizeProfile(a.InclusiveSize + b.InclusiveSize, a.ExclusiveSize + b.ExclusiveSize);
            public static AtomicSizeProfile operator +(AtomicSizeProfile a, int c) => new AtomicSizeProfile(a.InclusiveSize + c, a.ExclusiveSize + c);

        }
    }
}
