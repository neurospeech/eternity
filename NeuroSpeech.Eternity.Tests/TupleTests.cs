using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeuroSpeech.Eternity.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.Tests
{
    [TestClass]
    public class TupleTests
    {

        [TestMethod]
        public void Test()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ValueTupleConverter());

            var text = JsonSerializer.Serialize((5, true, 2.0), options);
            var (five, @true, two) = JsonSerializer.Deserialize<(int, bool, double)>(text, options);

            Assert.AreEqual(five, 5);
            Assert.AreEqual(@true, true);
            Assert.AreEqual(two, 2.0);
        }

        [TestMethod]
        public void DateTest()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ValueTupleConverter());

            var dt = new DateTime(2021, 1, 1,0,0,0, DateTimeKind.Utc);
            var dtJson = JsonSerializer.Serialize(dt);
            var st = JsonSerializer.Serialize(new[] { dtJson });
            var et = JsonSerializer.Deserialize<JsonElement[]>(st);
            var tt = et[0].GetString();
            var date = JsonSerializer.Deserialize<DateTime>(tt);
            Assert.AreEqual(dt, date);

        }

    }
}
