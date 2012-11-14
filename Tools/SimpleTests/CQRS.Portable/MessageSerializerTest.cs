using System;
using System.Collections.Generic;
using System.IO;
using Lokad.Cqrs;
using Lokad.Cqrs.Envelope;
using Lokad.Cqrs.Evil;
using NUnit.Framework;
using ServiceStack.Text;

namespace Sample.CQRS.Portable
{
    public class MessageSerializerTest
    {
        [Test]
        public void when_write_and_read_message()
        {
            var serializer = new TestMessageSerializer(new[] { typeof(Test1), typeof(Test2), });
            var msg = new Test1() { Name = "test name" };
            var stream = new MemoryStream();
            serializer.WriteMessage(msg, msg.GetType(), stream);
            stream.Seek(0, SeekOrigin.Begin);
            var readedMessage = serializer.ReadMessage(stream);

            Assert.AreEqual(typeof(Test1), readedMessage.GetType());
            Assert.AreEqual("test name", (readedMessage as Test1).Name);
        }

        [Test]
        public void when_write_and_read_attributes()
        {
            var serializer = new TestMessageSerializer(new[] { typeof(Test1), typeof(Test2), });
            var stream = new MemoryStream();
            serializer.WriteAttributes(new List<MessageAttribute> { new MessageAttribute("attr1", "val1"), new MessageAttribute("attr2", "val2") }, stream);
            stream.Seek(0, SeekOrigin.Begin);
            var readedAttributes = serializer.ReadAttributes(stream);

            Assert.AreEqual(2, readedAttributes.Length);
            Assert.AreEqual("attr1", readedAttributes[0].Key);
            Assert.AreEqual("val1", readedAttributes[0].Value);
            Assert.AreEqual("attr2", readedAttributes[1].Key);
            Assert.AreEqual("val2", readedAttributes[1].Value);
        }

        [Test]
        public void when_write_and_read_compactint()
        {
            var serializer = new TestMessageSerializer(new[] { typeof(Test1), typeof(Test2), });
            var stream = new MemoryStream();
            serializer.WriteCompactInt(10, stream);
            stream.Seek(0, SeekOrigin.Begin);
            var count = serializer.ReadCompactInt(stream);

            Assert.AreEqual(10, count);
        }

        [Test]
        public void when_write_and_read_attributes_and_messages()
        {
            var serializer = new TestMessageSerializer(new[] { typeof(Test1), typeof(Test2), });
            var msg = new Test1() { Name = "test name" };
            var stream = new MemoryStream();
            serializer.WriteAttributes(new List<MessageAttribute> { new MessageAttribute("attr1", "val1"), new MessageAttribute("attr2", "val2") }, stream);
            serializer.WriteMessage(msg, msg.GetType(), stream);
            stream.Seek(0, SeekOrigin.Begin);
            var readedAttributes = serializer.ReadAttributes(stream);
            var readedMessage = serializer.ReadMessage(stream);

            Assert.AreEqual(2, readedAttributes.Length);
            Assert.AreEqual("attr1", readedAttributes[0].Key);
            Assert.AreEqual("val1", readedAttributes[0].Value);
            Assert.AreEqual("attr2", readedAttributes[1].Key);
            Assert.AreEqual("val2", readedAttributes[1].Value);
            Assert.AreEqual(typeof(Test1), readedMessage.GetType());
            Assert.AreEqual("test name", (readedMessage as Test1).Name);
        }

        [Test]
        public void when_write_and_read_more_messages_with_attribute()
        {
            var serializer = new TestMessageSerializer(new[] { typeof(Test1), typeof(Test2), });
            var msg1 = new Test1() { Name = "message1" };
            var msg2 = new Test2() { Name = "message2" };
            var stream = new MemoryStream();
            serializer.WriteAttributes(new List<MessageAttribute> { new MessageAttribute("attr1", "val1"), new MessageAttribute("attr2", "val2") }, stream);
            serializer.WriteCompactInt(2, stream);
            serializer.WriteMessage(msg1, msg1.GetType(), stream);
            serializer.WriteMessage(msg2, msg2.GetType(), stream);
            stream.Seek(0, SeekOrigin.Begin);
            var readedAttributes = serializer.ReadAttributes(stream);
            var count = serializer.ReadCompactInt(stream);
            var readedMessage1 = serializer.ReadMessage(stream);
            var readedMessage2 = serializer.ReadMessage(stream);

            Assert.AreEqual(2, readedAttributes.Length);
            Assert.AreEqual("attr1", readedAttributes[0].Key);
            Assert.AreEqual("val1", readedAttributes[0].Value);
            Assert.AreEqual("attr2", readedAttributes[1].Key);
            Assert.AreEqual("val2", readedAttributes[1].Value);
            Assert.AreEqual(typeof(Test1), readedMessage1.GetType());
            Assert.AreEqual("message1", (readedMessage1 as Test1).Name);
            Assert.AreEqual(typeof(Test2), readedMessage2.GetType());
            Assert.AreEqual("message2", (readedMessage2 as Test2).Name);
        }

        class Test1
        {
            private int Id { get; set; }
            public string Name { get; set; }
        }

        class Test2
        {
            private int Id { get; set; }
            public string Name { get; set; }

            public void Method1()
            { }

            public int Method2()
            {
                return 1;
            }
        }
    }

    public class TestMessageSerializer : AbstractMessageSerializer
    {
        public TestMessageSerializer(ICollection<Type> knownTypes)
            : base(knownTypes)
        {
        }

        protected override Formatter PrepareFormatter(Type type)
        {
            var name = ContractEvil.GetContractReference(type);
            return new Formatter(name, type, s => JsonSerializer.DeserializeFromStream(type, s), (o, s) =>
            {
                using (var writer = new StreamWriter(s))
                {
                    writer.WriteLine();
                    writer.WriteLine(JsvFormatter.Format(JsonSerializer.SerializeToString(o, type)));
                }

            });
        }
    }

}