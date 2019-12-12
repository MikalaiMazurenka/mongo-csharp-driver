/* Copyright 2019-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.Serialization.Serializers;
using Xunit;

namespace MongoDB.Bson.Tests.Jira
{
    public class CSharp2860GenericListWithDictionaryElementsTest
    {
        [Fact]
        public void TestDeserializeWithCustomSerializerForGenericListWithDictionaryElements()
        {
            var classMap = new BsonClassMap<MyTestModel>();
            var memberInfo = typeof(MyTestModel).GetMember(nameof(MyTestModel.MyProperty)).Single();
            var memberMap = classMap.MapMember(memberInfo);
            var keySerializer = new MyDateTimeSerializer();
            var valueSerializer = new StringSerializer();
            var dictionarySerializer =
                new MyDictionarySerializer<IReadOnlyDictionary<DateTime, string>, DateTime, string>(
                    DictionaryRepresentation.ArrayOfDocuments, keySerializer, valueSerializer);
            var listSerializer =
                new MyListSerializer<IReadOnlyList<IReadOnlyDictionary<DateTime, string>>,
                    IReadOnlyDictionary<DateTime, string>>(dictionarySerializer);
            memberMap.SetSerializer(listSerializer);
            BsonClassMap.RegisterClassMap(classMap);
            var testModel = new MyTestModel
            {
                MyProperty = new List<IReadOnlyDictionary<DateTime, string>>
                {
                    new Dictionary<DateTime, string>
                    {
                        {DateTime.Now, "whatever"}
                    }
                }
            };

            var document = new BsonDocument();
            using (var writer = new BsonDocumentWriter(document))
            {
                BsonSerializer.Serialize(writer, testModel.GetType(), testModel);
                writer.Close();
            }
            var actualJson = document.ToJson();
            var expectedJson =
                "{ \"_t\" : \"MyTestModel\", \"MyProperty\" : { \"_t\" : \"ReadOnlyCollection`1\", \"_v\" : [[{ \"k\" : \"does-not-matter\", \"v\" : \"whatever\" }]] } }";
            Assert.Equal(expectedJson, actualJson);

            BsonSerializer.Deserialize<MyTestModel>(document);
        }

        // nested types
        private class MyTestModel
        {
            public IReadOnlyList<IReadOnlyDictionary<DateTime, string>> MyProperty { get; set; }
        }

        private class MyListSerializer<TCollection, TElement> : SerializerBase<TCollection>
            where TCollection : class, IEnumerable<TElement>
        {
            private readonly ReadOnlyCollectionSerializer<TElement> _underlyingSerializer;

            public MyListSerializer(IBsonSerializer<TElement> elementSerializer)
            {
                this._underlyingSerializer = new ReadOnlyCollectionSerializer<TElement>(elementSerializer);
            }

            /// <inheritdoc />
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args,
                TCollection value)
            {
                if (value == null)
                {
                    context.Writer.WriteNull();
                    return;
                }

                this._underlyingSerializer.Serialize(context, args, new ReadOnlyCollection<TElement>(value.ToList()));
            }

            /// <inheritdoc />
            public override TCollection Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                if (context.Reader.State != BsonReaderState.Type && context.Reader.CurrentBsonType == BsonType.Null)
                {
                    context.Reader.ReadNull();
                    return null;
                }

                var collection = this._underlyingSerializer.Deserialize(context, args);
                var result = collection.ToList() as TCollection;
                return result;
            }
        }

        private class MyDictionarySerializer<TDictionary, TKey, TValue> : SerializerBase<TDictionary>
            where TDictionary : class, IEnumerable<KeyValuePair<TKey, TValue>>
        {
            private readonly DictionaryInterfaceImplementerSerializer<Dictionary<TKey, TValue>> _underlyingSerializer;

            public MyDictionarySerializer(DictionaryRepresentation dictionaryRepresentation,
                IBsonSerializer keySerializer, IBsonSerializer valueSerializer)
            {
                this._underlyingSerializer =
                    new DictionaryInterfaceImplementerSerializer<Dictionary<TKey, TValue>>(dictionaryRepresentation,
                        keySerializer, valueSerializer);
            }

            /// <inheritdoc />
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args,
                TDictionary value)
            {
                if (value == null)
                {
                    context.Writer.WriteNull();
                    return;
                }

                this._underlyingSerializer.Serialize(context, args,
                    ((IDictionary<TKey, TValue>)value).ToDictionary(_ => _.Key, _ => _.Value));
            }

            /// <inheritdoc />
            public override TDictionary Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                if (context.Reader.State != BsonReaderState.Type && context.Reader.CurrentBsonType == BsonType.Null)
                {
                    context.Reader.ReadNull();
                    return null;
                }

                var dictionary = this._underlyingSerializer.Deserialize(context, args);
                var result = new ReadOnlyDictionary<TKey, TValue>(dictionary) as TDictionary;
                return result;
            }
        }

        private class MyDateTimeSerializer : SerializerBase<DateTime>
        {
            private const string DoesNotMatter = "does-not-matter";

            /// <inheritdoc />
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTime value)
            {
                context.Writer.WriteString(DoesNotMatter);
            }

            /// <inheritdoc />
            public override DateTime Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                var type = context.Reader.GetCurrentBsonType();
                if (type == BsonType.String && context.Reader.ReadString() == DoesNotMatter)
                {
                    return DateTime.Now;
                }

                throw new NotSupportedException();
            }
        }
    }

    public class CSharp2860GenericListWithClassElementsTest
    {
        [Fact]
        public static void TestDeserializeWithCustomSerializerForGenericListWithClassElements()
        {
            var dictionarySerializer =
                new MyDictionarySerializer<IReadOnlyDictionary<DateTime, string>, DateTime, string>(
                    DictionaryRepresentation.ArrayOfDocuments,
                    new MyDateTimeSerializer(),
                    new StringSerializer());
            var listSerializer = new MyListSerializer<IReadOnlyList<MyListItem>, MyListItem>();

            BsonClassMap.RegisterClassMap<MyTestModel>(cm =>
            {
                cm.AutoMap();
                cm.GetMemberMap(c => c.MyProperty).SetSerializer(listSerializer);
            });
            BsonClassMap.RegisterClassMap<MyListItem>(cm =>
            {
                cm.AutoMap();
                cm.GetMemberMap(c => c.Value).SetSerializer(dictionarySerializer);
            });

            var expected = new MyTestModel
            {
                MyProperty = new List<MyListItem>
                {
                    new MyListItem
                    {
                        Value = new Dictionary<DateTime, string>
                        {
                            {DateTime.Now, "whatever"}
                        }
                    }
                }
            };

            var document = new BsonDocument();
            using (var writer = new BsonDocumentWriter(document))
            {
                BsonSerializer.Serialize(writer, expected.GetType(), expected);
                writer.Close();
            }

            var actualJson = document.ToJson();
            var expectedJson =
                "{ \"_t\" : \"MyTestModel\", \"MyProperty\" : { \"_t\" : \"ReadOnlyCollection`1\", \"_v\" : [{ \"Value\" : [{ \"k\" : \"does-not-matter\", \"v\" : \"whatever\" }] }] } }";
            Assert.Equal(expectedJson, actualJson);

            BsonSerializer.Deserialize<MyTestModel>(document);
        }

        private class MyTestModel
        {
            public IReadOnlyList<MyListItem> MyProperty { get; set; }
        }

        private class MyListItem
        {
            public IReadOnlyDictionary<DateTime, string> Value { get; set; }
        }

        private class MyListSerializer<TCollection, TElement> : SerializerBase<TCollection>
            where TCollection : class, IEnumerable<TElement>
        {
            private readonly ReadOnlyCollectionSerializer<TElement> _underlyingSerializer;

            public MyListSerializer()
            {
                this._underlyingSerializer = new ReadOnlyCollectionSerializer<TElement>();
            }

            /// <inheritdoc />
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args,
                TCollection value)
            {
                if (value == null)
                {
                    context.Writer.WriteNull();
                    return;
                }

                this._underlyingSerializer.Serialize(context, args, new ReadOnlyCollection<TElement>(value.ToList()));
            }

            /// <inheritdoc />
            public override TCollection Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                if (context.Reader.State != BsonReaderState.Type && context.Reader.CurrentBsonType == BsonType.Null)
                {
                    context.Reader.ReadNull();
                    return null;
                }

                var collection = this._underlyingSerializer.Deserialize(context, args);
                var result = collection.ToList() as TCollection;
                return result;
            }
        }

        private class MyDictionarySerializer<TDictionary, TKey, TValue> : SerializerBase<TDictionary>
            where TDictionary : class, IEnumerable<KeyValuePair<TKey, TValue>>
        {
            private readonly DictionaryInterfaceImplementerSerializer<Dictionary<TKey, TValue>> _underlyingSerializer;

            public MyDictionarySerializer(DictionaryRepresentation dictionaryRepresentation,
                IBsonSerializer keySerializer, IBsonSerializer valueSerializer)
            {
                this._underlyingSerializer =
                    new DictionaryInterfaceImplementerSerializer<Dictionary<TKey, TValue>>(dictionaryRepresentation,
                        keySerializer, valueSerializer);
            }

            /// <inheritdoc />
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args,
                TDictionary value)
            {
                if (value == null)
                {
                    context.Writer.WriteNull();
                    return;
                }

                this._underlyingSerializer.Serialize(context, args,
                    ((IDictionary<TKey, TValue>) value).ToDictionary(_ => _.Key, _ => _.Value));
            }

            /// <inheritdoc />
            public override TDictionary Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                if (context.Reader.State != BsonReaderState.Type && context.Reader.CurrentBsonType == BsonType.Null)
                {
                    context.Reader.ReadNull();
                    return null;
                }

                var dictionary = this._underlyingSerializer.Deserialize(context, args);
                var result = new ReadOnlyDictionary<TKey, TValue>(dictionary) as TDictionary;
                return result;
            }
        }

        private class MyDateTimeSerializer : SerializerBase<DateTime>
        {
            private const string DoesNotMatter = "does-not-matter";

            /// <inheritdoc />
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTime value)
            {
                context.Writer.WriteString(DoesNotMatter);
            }

            /// <inheritdoc />
            public override DateTime Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                var type = context.Reader.GetCurrentBsonType();
                if (type == BsonType.String && context.Reader.ReadString() == DoesNotMatter)
                {
                    return DateTime.Now;
                }

                throw new NotSupportedException();
            }
        }
    }

    public class CSharp2860GenericReadonlyListWithDictionaryElementsTest
    {
        [Fact]
        public void TestDeserializeWithCustomSerializerForGenericListWithDictionaryElements()
        {
            var classMap = new BsonClassMap<MyTestModel>();
            var memberInfo = typeof(MyTestModel).GetMember(nameof(MyTestModel.MyProperty)).Single();
            var memberMap = classMap.MapMember(memberInfo);
            var keySerializer = new MyDateTimeSerializer();
            var valueSerializer = new StringSerializer();
            var dictionarySerializer =
                new MyDictionarySerializer<IReadOnlyDictionary<DateTime, string>, DateTime, string>(
                    DictionaryRepresentation.ArrayOfDocuments, keySerializer, valueSerializer);
            var listSerializer =
                new MyListSerializer<ReadOnlyCollection<IReadOnlyDictionary<DateTime, string>>,
                    IReadOnlyDictionary<DateTime, string>>(dictionarySerializer);
            memberMap.SetSerializer(listSerializer);
            BsonClassMap.RegisterClassMap(classMap);
            var testModel = new MyTestModel
            {
                MyProperty = new List<IReadOnlyDictionary<DateTime, string>>
                {
                    new Dictionary<DateTime, string>
                    {
                        {DateTime.Now, "whatever"}
                    }
                }.AsReadOnly()
            };

            var document = new BsonDocument();
            using (var writer = new BsonDocumentWriter(document))
            {
                BsonSerializer.Serialize(writer, testModel.GetType(), testModel);
                writer.Close();
            }
            var actualJson = document.ToJson();
            var expectedJson =
                "{ \"_t\" : \"MyTestModel\", \"MyProperty\" : [[{ \"k\" : \"does-not-matter\", \"v\" : \"whatever\" }]] }";
            Assert.Equal(expectedJson, actualJson);

            BsonSerializer.Deserialize<MyTestModel>(document);
        }

        // nested types
        private class MyTestModel
        {
            public ReadOnlyCollection<IReadOnlyDictionary<DateTime, string>> MyProperty { get; set; }
        }

        private class MyListSerializer<TCollection, TElement> : SerializerBase<TCollection>
            where TCollection : class, IEnumerable<TElement>
        {
            private readonly ReadOnlyCollectionSerializer<TElement> _underlyingSerializer;

            public MyListSerializer(IBsonSerializer<TElement> elementSerializer)
            {
                this._underlyingSerializer = new ReadOnlyCollectionSerializer<TElement>(elementSerializer);
            }

            /// <inheritdoc />
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args,
                TCollection value)
            {
                if (value == null)
                {
                    context.Writer.WriteNull();
                    return;
                }

                this._underlyingSerializer.Serialize(context, args, new ReadOnlyCollection<TElement>(value.ToList()));
            }

            /// <inheritdoc />
            public override TCollection Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                if (context.Reader.State != BsonReaderState.Type && context.Reader.CurrentBsonType == BsonType.Null)
                {
                    context.Reader.ReadNull();
                    return null;
                }

                var collection = this._underlyingSerializer.Deserialize(context, args);
                var result = collection.ToList() as TCollection;
                return result;
            }
        }

        private class MyDictionarySerializer<TDictionary, TKey, TValue> : SerializerBase<TDictionary>
            where TDictionary : class, IEnumerable<KeyValuePair<TKey, TValue>>
        {
            private readonly DictionaryInterfaceImplementerSerializer<Dictionary<TKey, TValue>> _underlyingSerializer;

            public MyDictionarySerializer(DictionaryRepresentation dictionaryRepresentation,
                IBsonSerializer keySerializer, IBsonSerializer valueSerializer)
            {
                this._underlyingSerializer =
                    new DictionaryInterfaceImplementerSerializer<Dictionary<TKey, TValue>>(dictionaryRepresentation,
                        keySerializer, valueSerializer);
            }

            /// <inheritdoc />
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args,
                TDictionary value)
            {
                if (value == null)
                {
                    context.Writer.WriteNull();
                    return;
                }

                this._underlyingSerializer.Serialize(context, args,
                    ((IDictionary<TKey, TValue>)value).ToDictionary(_ => _.Key, _ => _.Value));
            }

            /// <inheritdoc />
            public override TDictionary Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                if (context.Reader.State != BsonReaderState.Type && context.Reader.CurrentBsonType == BsonType.Null)
                {
                    context.Reader.ReadNull();
                    return null;
                }

                var dictionary = this._underlyingSerializer.Deserialize(context, args);
                var result = new ReadOnlyDictionary<TKey, TValue>(dictionary) as TDictionary;
                return result;
            }
        }

        private class MyDateTimeSerializer : SerializerBase<DateTime>
        {
            private const string DoesNotMatter = "does-not-matter";

            /// <inheritdoc />
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTime value)
            {
                context.Writer.WriteString(DoesNotMatter);
            }

            /// <inheritdoc />
            public override DateTime Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                var type = context.Reader.GetCurrentBsonType();
                if (type == BsonType.String && context.Reader.ReadString() == DoesNotMatter)
                {
                    return DateTime.Now;
                }

                throw new NotSupportedException();
            }
        }
    }

    public class CSharp2860GenericListWithDictionaryElementsTestUsingImpliedImplementationInterfaceSerializer
    {
        [Fact]
        public void TestDeserializeWithCustomSerializerForGenericListUsingImpliedImplementationInterfaceSerializer()
        {
            var classMap = new BsonClassMap<MyTestModel>();
            var memberInfo = typeof(MyTestModel).GetMember(nameof(MyTestModel.MyProperty)).Single();
            var memberMap = classMap.MapMember(memberInfo);
            var keySerializer = new MyDateTimeSerializer();
            var valueSerializer = new StringSerializer();
            var dictionarySerializer =
                new MyDictionarySerializer<IReadOnlyDictionary<DateTime, string>, DateTime, string>(
                    DictionaryRepresentation.ArrayOfDocuments, keySerializer, valueSerializer);
            var listSerializer =
                new MyListSerializer<IReadOnlyList<IReadOnlyDictionary<DateTime, string>>,
                    IReadOnlyDictionary<DateTime, string>>(dictionarySerializer);
            memberMap.SetSerializer(listSerializer);
            BsonClassMap.RegisterClassMap(classMap);
            var testModel = new MyTestModel
            {
                MyProperty = new List<IReadOnlyDictionary<DateTime, string>>
                {
                    new Dictionary<DateTime, string>
                    {
                        {DateTime.Now, "whatever"}
                    }
                }
            };

            var document = new BsonDocument();
            using (var writer = new BsonDocumentWriter(document))
            {
                BsonSerializer.Serialize(writer, testModel.GetType(), testModel);
                writer.Close();
            }
            var actualJson = document.ToJson();
            var expectedJson =
                "{ \"_t\" : \"MyTestModel\", \"MyProperty\" : [[{ \"k\" : \"does-not-matter\", \"v\" : \"whatever\" }]] }";
            Assert.Equal(expectedJson, actualJson);

            BsonSerializer.Deserialize<MyTestModel>(document);
        }

        // nested types
        private class MyTestModel
        {
            public IReadOnlyList<IReadOnlyDictionary<DateTime, string>> MyProperty { get; set; }
        }

        private class MyListSerializer<TCollection, TElement> : SerializerBase<TCollection>
            where TCollection : class, IEnumerable<TElement>
        {
            private readonly ImpliedImplementationInterfaceSerializer<IReadOnlyList<TElement>, ReadOnlyCollection<TElement>> _underlyingSerializer;

            public MyListSerializer(IBsonSerializer<TElement> elementSerializer)
            {
                this._underlyingSerializer =
                    new ImpliedImplementationInterfaceSerializer<IReadOnlyList<TElement>, ReadOnlyCollection<TElement>>(
                        new ReadOnlyCollectionSerializer<TElement>(elementSerializer));
            }

            /// <inheritdoc />
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args,
                TCollection value)
            {
                if (value == null)
                {
                    context.Writer.WriteNull();
                    return;
                }

                this._underlyingSerializer.Serialize(context, args, new ReadOnlyCollection<TElement>(value.ToList()));
            }

            /// <inheritdoc />
            public override TCollection Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                if (context.Reader.State != BsonReaderState.Type && context.Reader.CurrentBsonType == BsonType.Null)
                {
                    context.Reader.ReadNull();
                    return null;
                }

                var collection = this._underlyingSerializer.Deserialize(context, args);
                var result = collection.ToList() as TCollection;
                return result;
            }
        }

        private class MyDictionarySerializer<TDictionary, TKey, TValue> : SerializerBase<TDictionary>
            where TDictionary : class, IEnumerable<KeyValuePair<TKey, TValue>>
        {
            private readonly DictionaryInterfaceImplementerSerializer<Dictionary<TKey, TValue>> _underlyingSerializer;

            public MyDictionarySerializer(DictionaryRepresentation dictionaryRepresentation,
                IBsonSerializer keySerializer, IBsonSerializer valueSerializer)
            {
                this._underlyingSerializer =
                    new DictionaryInterfaceImplementerSerializer<Dictionary<TKey, TValue>>(dictionaryRepresentation,
                        keySerializer, valueSerializer);
            }

            /// <inheritdoc />
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args,
                TDictionary value)
            {
                if (value == null)
                {
                    context.Writer.WriteNull();
                    return;
                }

                this._underlyingSerializer.Serialize(context, args,
                    ((IDictionary<TKey, TValue>)value).ToDictionary(_ => _.Key, _ => _.Value));
            }

            /// <inheritdoc />
            public override TDictionary Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                if (context.Reader.State != BsonReaderState.Type && context.Reader.CurrentBsonType == BsonType.Null)
                {
                    context.Reader.ReadNull();
                    return null;
                }

                var dictionary = this._underlyingSerializer.Deserialize(context, args);
                var result = new ReadOnlyDictionary<TKey, TValue>(dictionary) as TDictionary;
                return result;
            }
        }

        private class MyDateTimeSerializer : SerializerBase<DateTime>
        {
            private const string DoesNotMatter = "does-not-matter";

            /// <inheritdoc />
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTime value)
            {
                context.Writer.WriteString(DoesNotMatter);
            }

            /// <inheritdoc />
            public override DateTime Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                var type = context.Reader.GetCurrentBsonType();
                if (type == BsonType.String && context.Reader.ReadString() == DoesNotMatter)
                {
                    return DateTime.Now;
                }

                throw new NotSupportedException();
            }
        }
    }
    
    public class CSharp2860GenericListWithDictionaryElementsTestUsingImpliedImplementationInterfaceSerializerForProperty
    {
        [Fact]
        public void TestDeserializeWithCustomSerializerForGenericListUsingImpliedImplementationInterfaceSerializerForProperty()
        {
            var dictionarySerializer =
                new MyDictionarySerializer<IReadOnlyDictionary<DateTime, string>, DateTime, string>(
                    DictionaryRepresentation.ArrayOfDocuments,
                    new MyDateTimeSerializer(),
                    new StringSerializer());
            var impliedImplementationInterfaceSerializer =
                new ImpliedImplementationInterfaceSerializer<
                    IReadOnlyList<IReadOnlyDictionary<DateTime, string>>,
                    List<IReadOnlyDictionary<DateTime, string>>>(
                    new MyListSerializer<List<IReadOnlyDictionary<DateTime, string>>,
                        IReadOnlyDictionary<DateTime, string>>(dictionarySerializer));

            BsonClassMap.RegisterClassMap<MyTestModel>(cm =>
            {
                cm.AutoMap();
                cm.GetMemberMap(c => c.MyProperty).SetSerializer(impliedImplementationInterfaceSerializer);
            });

            var testModel = new MyTestModel
            {
                MyProperty = new List<IReadOnlyDictionary<DateTime, string>>
                {
                    new Dictionary<DateTime, string>
                    {
                        {DateTime.Now, "whatever"}
                    }
                }
            };

            var document = new BsonDocument();
            using (var writer = new BsonDocumentWriter(document))
            {
                BsonSerializer.Serialize(writer, testModel.GetType(), testModel);
                writer.Close();
            }
            var actualJson = document.ToJson();
            var expectedJson =
                "{ \"_t\" : \"MyTestModel\", \"MyProperty\" : { \"_t\" : \"ReadOnlyCollection`1\", \"_v\" : [[{ \"k\" : \"does-not-matter\", \"v\" : \"whatever\" }]] } }";
            Assert.Equal(expectedJson, actualJson);

            BsonSerializer.Deserialize<MyTestModel>(document);
        }

        // nested types
        private class MyTestModel
        {
            public IReadOnlyList<IReadOnlyDictionary<DateTime, string>> MyProperty { get; set; }
        }

        private class MyListSerializer<TCollection, TElement> : SerializerBase<TCollection>
            where TCollection : class, IEnumerable<TElement>
        {
            private readonly ReadOnlyCollectionSerializer<TElement> _underlyingSerializer;

            public MyListSerializer(IBsonSerializer<TElement> elementSerializer)
            {
                this._underlyingSerializer = new ReadOnlyCollectionSerializer<TElement>(elementSerializer);
            }

            /// <inheritdoc />
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args,
                TCollection value)
            {
                if (value == null)
                {
                    context.Writer.WriteNull();
                    return;
                }

                this._underlyingSerializer.Serialize(context, args, new ReadOnlyCollection<TElement>(value.ToList()));
            }

            /// <inheritdoc />
            public override TCollection Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                if (context.Reader.State != BsonReaderState.Type && context.Reader.CurrentBsonType == BsonType.Null)
                {
                    context.Reader.ReadNull();
                    return null;
                }

                var collection = this._underlyingSerializer.Deserialize(context, args);
                var result = collection.ToList() as TCollection;
                return result;
            }
        }

        private class MyDictionarySerializer<TDictionary, TKey, TValue> : SerializerBase<TDictionary>
            where TDictionary : class, IEnumerable<KeyValuePair<TKey, TValue>>
        {
            private readonly DictionaryInterfaceImplementerSerializer<Dictionary<TKey, TValue>> _underlyingSerializer;

            public MyDictionarySerializer(DictionaryRepresentation dictionaryRepresentation,
                IBsonSerializer keySerializer, IBsonSerializer valueSerializer)
            {
                this._underlyingSerializer =
                    new DictionaryInterfaceImplementerSerializer<Dictionary<TKey, TValue>>(dictionaryRepresentation,
                        keySerializer, valueSerializer);
            }

            /// <inheritdoc />
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args,
                TDictionary value)
            {
                if (value == null)
                {
                    context.Writer.WriteNull();
                    return;
                }

                this._underlyingSerializer.Serialize(context, args,
                    ((IDictionary<TKey, TValue>)value).ToDictionary(_ => _.Key, _ => _.Value));
            }

            /// <inheritdoc />
            public override TDictionary Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                if (context.Reader.State != BsonReaderState.Type && context.Reader.CurrentBsonType == BsonType.Null)
                {
                    context.Reader.ReadNull();
                    return null;
                }

                var dictionary = this._underlyingSerializer.Deserialize(context, args);
                var result = new ReadOnlyDictionary<TKey, TValue>(dictionary) as TDictionary;
                return result;
            }
        }

        private class MyDateTimeSerializer : SerializerBase<DateTime>
        {
            private const string DoesNotMatter = "does-not-matter";

            /// <inheritdoc />
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTime value)
            {
                context.Writer.WriteString(DoesNotMatter);
            }

            /// <inheritdoc />
            public override DateTime Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                var type = context.Reader.GetCurrentBsonType();
                if (type == BsonType.String && context.Reader.ReadString() == DoesNotMatter)
                {
                    return DateTime.Now;
                }

                throw new NotSupportedException();
            }
        }
    }
}