using System;
using System.Collections.Generic;
using System.IO;
using Lokad.Cqrs.Envelope;
using Lokad.Cqrs.Evil;
using NUnit.Framework;
using ServiceStack.Text;

namespace Sample.CQRS.Portable
{
    public class MessageSerializerTest
    {
        [Test]
         public void when_write_message()
         {
             
         }
    }

    public class TestMessageSerializer:AbstractMessageSerializer
    {
        public TestMessageSerializer(ICollection<Type> knownTypes) : base(knownTypes)
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